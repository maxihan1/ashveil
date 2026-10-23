// 캐릭터 목록·생성·삭제·조회, 위치 저장, 인벤토리·장착, PK 기록 API
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashveil.Server.Endpoints;

public static class CharacterEndpoints
{
    public static void MapCharacterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/characters").WithTags("Characters").RequireAuthorization();

        group.MapGet("/", async (HttpContext http, GameDbContext db) =>
        {
            var accountId = http.AccountId();
            var list = await db.Characters.AsNoTracking()
                .Where(c => c.AccountId == accountId && c.DeletedAt == null)
                .OrderBy(c => c.Id).ToListAsync();
            return list.Select(Map.Summary).ToList();
        });

        group.MapPost("/", async (CreateCharacterRequest req, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var accountId = http.AccountId();
            var name = req.Name?.Trim() ?? "";
            if (!NameRules.IsValidCharacterName(name))
                throw GameErrors.Validation("캐릭터 이름은 2~12자의 한글, 영문, 숫자만 쓸 수 있습니다.");
            if (!Enum.IsDefined(req.ClassType) || !Enum.IsDefined(req.Gender))
                throw GameErrors.Validation("직업 또는 성별 값이 올바르지 않습니다.");

            var now = clock.GetUtcNow().UtcDateTime;
            var character = new Character
            {
                AccountId = accountId,
                Name = name,
                NameKey = name.ToLowerInvariant(),
                ClassType = req.ClassType,
                Gender = req.Gender,
                Level = 1,
                PosX = ZoneMap.RespawnX,
                PosZ = ZoneMap.RespawnZ,
                CreatedAt = now,
            };

            try
            {
                await db.InTransactionAsync(async () =>
                {
                    // 계정 행을 잠가 "최대 3개" 검사와 생성 사이에 다른 요청이 끼지 못하게 한다.
                    await db.Database.ExecuteSqlAsync($"SELECT id FROM accounts WHERE id = {accountId} FOR UPDATE");
                    var count = await db.Characters.CountAsync(c => c.AccountId == accountId && c.DeletedAt == null);
                    if (count >= NameRules.MaxCharactersPerAccount) throw GameErrors.Conflict(ErrorCodes.CharacterLimit);

                    db.Characters.Add(character);
                    await db.SaveChangesAsync();

                    db.ChangeGold(character, ItemCatalog.StarterGold, LedgerReasons.CharacterCreated, "Character", character.Id.ToString(), now);
                    db.AddStarterItems(character, now);
                });
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw GameErrors.Conflict(ErrorCodes.NameTaken);
            }

            return Results.Created($"/characters/{character.Id}", await db.CharacterDtoAsync(character, now));
        });

        group.MapDelete("/{id:long}", async (long id, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var accountId = http.AccountId();
            await db.InTransactionAsync(async () =>
            {
                var character = await db.LockOwnedCharacterAsync(id, accountId);
                character.DeletedAt = clock.GetUtcNow().UtcDateTime;
                await db.Items.Where(i => i.OwnerKind == OwnerKind.Character && i.OwnerId == id).ExecuteDeleteAsync();
            });
            return Results.NoContent();
        });

        group.MapGet("/{id:long}", async (long id, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var character = await db.GetOwnedCharacterAsync(id, http.AccountId());
            return await db.CharacterDtoAsync(character, clock.GetUtcNow().UtcDateTime);
        });

        group.MapPut("/{id:long}/position", async (long id, PositionRequest req, HttpContext http, GameDbContext db) =>
        {
            GameOps.RequireFinite(req.X, req.Y, req.Z);
            if (req.X < 0 || req.X > ZoneMap.WorldSize || req.Z < 0 || req.Z > ZoneMap.WorldSize)
                throw GameErrors.Validation("월드 밖의 좌표입니다.");
            await db.InTransactionAsync(async () =>
            {
                var character = await db.LockOwnedCharacterAsync(id, http.AccountId());
                character.PosX = req.X;
                character.PosY = req.Y;
                character.PosZ = req.Z;
            });
            return Results.NoContent();
        });

        group.MapGet("/{id:long}/inventory", async (long id, HttpContext http, GameDbContext db) =>
        {
            var character = await db.GetOwnedCharacterAsync(id, http.AccountId());
            return await db.InventoryDtoAsync(character);
        });

        group.MapPost("/{id:long}/equip", (long id, EquipRequest req, HttpContext http, GameDbContext db) =>
            ChangeEquipAsync(db, id, http.AccountId(), req.ItemId, equip: true));

        group.MapPost("/{id:long}/unequip", (long id, EquipRequest req, HttpContext http, GameDbContext db) =>
            ChangeEquipAsync(db, id, http.AccountId(), req.ItemId, equip: false));

        // 소모품(물약) 사용. HP·MP 는 클라이언트 런타임 상태이므로 회복량만 돌려준다.
        group.MapPost("/{id:long}/use-item", async (long id, UseItemRequest req, HttpContext http, GameDbContext db) =>
        {
            Character character = null!;
            ItemTemplate template = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(id, http.AccountId());
                var item = await db.Items.SingleOrDefaultAsync(i =>
                    i.Id == req.ItemId && i.OwnerKind == OwnerKind.Character && i.OwnerId == character.Id)
                    ?? throw GameErrors.NotFound("아이템");
                template = ItemCatalog.Get(item.TemplateId);
                if (template.Type != ItemType.Consumable) throw GameErrors.Conflict(ErrorCodes.NotUsable);
                if (item.Quantity > 1) item.Quantity -= 1;
                else db.Items.Remove(item);
            });
            return new UseItemResponse
            {
                TemplateId = template.Id, HealHp = template.HealHp, HealMp = template.HealMp,
                Inventory = await db.InventoryDtoAsync(character),
            };
        });

        group.MapGet("/{id:long}/pk-history", async (long id, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var character = await db.GetOwnedCharacterAsync(id, http.AccountId());
            var records = await db.PkRecords.AsNoTracking()
                .Where(r => (r.KillerKind == EntityKind.Character && r.KillerId == id) || (r.VictimKind == EntityKind.Character && r.VictimId == id))
                .OrderByDescending(r => r.Id).Take(50).ToListAsync();
            var history = await db.AlignmentHistories.AsNoTracking()
                .Where(h => h.EntityKind == EntityKind.Character && h.EntityId == id)
                .OrderByDescending(h => h.Id).Take(50).ToListAsync();

            return new PkHistoryDto
            {
                Kills = character.PkKills,
                Deaths = character.PkDeaths,
                Alignment = Map.Alignment(character.Alignment, character.PurpleUntil, clock.GetUtcNow().UtcDateTime),
                Records = records.Select(r => new PkRecordDto
                {
                    KillerKind = r.KillerKind, KillerName = r.KillerName, VictimKind = r.VictimKind, VictimName = r.VictimName,
                    VictimWasInnocent = r.VictimWasInnocent, AlignmentDelta = r.AlignmentDelta, CreatedAt = r.CreatedAt,
                }).ToList(),
                AlignmentHistory = history.Select(h => new AlignmentHistoryDto
                {
                    Delta = h.Delta, After = h.After, Reason = h.Reason, CreatedAt = h.CreatedAt,
                }).ToList(),
            };
        });
    }

    private static async Task<EquipResponse> ChangeEquipAsync(GameDbContext db, long characterId, long accountId, long itemId, bool equip)
    {
        Character character = null!;
        List<Item> items = null!;
        await db.InTransactionAsync(async () =>
        {
            character = await db.LockOwnedCharacterAsync(characterId, accountId);
            items = await db.LoadItemsAsync(OwnerKind.Character, characterId);
            var item = items.FirstOrDefault(i => i.Id == itemId) ?? throw GameErrors.NotFound("아이템");
            var slot = ItemCatalog.Get(item.TemplateId).Slot ?? throw GameErrors.Conflict(ErrorCodes.NotEquippable);

            if (!equip)
            {
                item.EquippedSlot = null;
                return;
            }
            foreach (var other in items.Where(i => i.EquippedSlot == slot && i.Id != item.Id)) other.EquippedSlot = null;
            item.EquippedSlot = slot;
        });

        return new EquipResponse
        {
            Inventory = Map.Inventory(character, items),
            Stats = Map.Stats(GameOps.ComputeStats(character.ClassType, character.Level, items)),
        };
    }
}
