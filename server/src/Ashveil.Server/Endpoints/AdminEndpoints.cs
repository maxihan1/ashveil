// QA 환경 전용 어드민 API: 아무 캐릭터 조회·조작, 결과 고정, 테스트 데이터 정리, 확률·스폰 설정 (X-Admin-Key 인증)
using System.Security.Cryptography;
using System.Text;
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Endpoints;

public static class AdminEndpoints
{
    public const string KeyHeader = "X-Admin-Key";

    public static void MapAdminEndpoints(this WebApplication app)
    {
        var key = app.Configuration["Admin:Key"] ?? "";
        if (!app.Environment.IsEnvironment(GmEndpoints.QaEnvironment) || key.Length < 16)
        {
            // QA 가 아니거나 키가 없으면 어드민 경로 전체를 막는다.
            app.Map("/admin/{**rest}", () => GameErrors.ToResult(ErrorCodes.AdminDisabled, 404)).ExcludeFromDescription();
            return;
        }

        var expected = Encoding.UTF8.GetBytes(key);
        var admin = app.MapGroup("/admin").WithTags("Admin (QA only)").AddEndpointFilter(async (ctx, next) =>
        {
            var given = Encoding.UTF8.GetBytes(ctx.HttpContext.Request.Headers[KeyHeader].ToString());
            if (!CryptographicOperations.FixedTimeEquals(given, expected))
                return GameErrors.ToResult(ErrorCodes.Unauthorized, 401, $"{KeyHeader} 헤더가 올바르지 않습니다.");
            return await next(ctx);
        });

        MapQueries(admin);
        MapCharacterChanges(admin);
        MapTestControls(admin);
        MapSpawns(admin);
    }

    /// <summary>어드민 웹 화면(wwwroot/backoffice/index.html). 화면은 키를 담지 않고, 사용자가 입력한 키로 어드민 API 를 부른다.</summary>
    public static void MapBackoffice(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment(GmEndpoints.QaEnvironment)) return;
        app.MapGet("/backoffice", (IWebHostEnvironment env) =>
        {
            var file = env.WebRootFileProvider.GetFileInfo("backoffice/index.html");
            return file.Exists ? Results.Stream(file.CreateReadStream(), "text/html; charset=utf-8") : Results.NotFound();
        }).ExcludeFromDescription();
    }

    private static void MapQueries(RouteGroupBuilder admin)
    {
        admin.MapGet("/item-templates", () => ItemCatalog.All.Select(t => new { t.Id, t.Name, Type = t.Type.ToString() }).ToList());

        admin.MapGet("/accounts", async (string? prefix, int? limit, GameDbContext db) =>
        {
            var query = db.Accounts.AsNoTracking();
            if (!string.IsNullOrEmpty(prefix)) query = query.Where(a => a.Username.StartsWith(prefix));
            return await query.OrderByDescending(a => a.Id).Take(Math.Clamp(limit ?? 50, 1, 500))
                .Select(a => new AdminAccountDto
                {
                    Id = a.Id, Username = a.Username, CreatedAt = a.CreatedAt,
                    CharacterCount = db.Characters.Count(c => c.AccountId == a.Id && c.DeletedAt == null),
                }).ToListAsync();
        });

        admin.MapGet("/characters", async (string? name, long? accountId, bool? includeDeleted, int? limit, GameDbContext db) =>
        {
            var query = from c in db.Characters.AsNoTracking()
                        join a in db.Accounts.AsNoTracking() on c.AccountId equals a.Id
                        select new { c, a.Username };
            if (includeDeleted != true) query = query.Where(x => x.c.DeletedAt == null);
            if (!string.IsNullOrEmpty(name)) query = query.Where(x => x.c.NameKey.Contains(name.ToLower()));
            if (accountId.HasValue) query = query.Where(x => x.c.AccountId == accountId.Value);
            var list = await query.OrderByDescending(x => x.c.Id).Take(Math.Clamp(limit ?? 50, 1, 500)).ToListAsync();
            return list.Select(x => ToAdmin(x.c, x.Username)).ToList();
        });

        admin.MapGet("/characters/{id:long}", async (long id, GameDbContext db, TimeProvider clock) =>
            await db.CharacterDtoAsync(await FindCharacterAsync(db, id), clock.GetUtcNow().UtcDateTime));

        admin.MapGet("/characters/{id:long}/summary", async (long id, GameDbContext db) =>
        {
            var character = await FindCharacterAsync(db, id);
            var username = await db.Accounts.Where(a => a.Id == character.AccountId).Select(a => a.Username).SingleAsync();
            return ToAdmin(character, username);
        });

        admin.MapGet("/characters/{id:long}/inventory", async (long id, GameDbContext db) =>
            await db.InventoryDtoAsync(await FindCharacterAsync(db, id)));

        admin.MapGet("/characters/{id:long}/ledger", async (long id, int? limit, GameDbContext db) =>
        {
            await FindCharacterAsync(db, id);
            var entries = await db.LedgerEntries.AsNoTracking().Where(e => e.CharacterId == id)
                .OrderByDescending(e => e.Id).Take(Math.Clamp(limit ?? 100, 1, 1000)).ToListAsync();
            return entries.Select(Map.Ledger).ToList();
        });

        admin.MapGet("/characters/{id:long}/enhance-history", async (long id, int? limit, GameDbContext db) =>
        {
            await FindCharacterAsync(db, id);
            return await db.EnhanceHistories.AsNoTracking().Where(h => h.CharacterId == id)
                .OrderByDescending(h => h.Id).Take(Math.Clamp(limit ?? 100, 1, 1000))
                .Select(h => new AdminEnhanceHistoryDto
                {
                    Id = h.Id, ItemId = h.ItemId, TemplateId = h.TemplateId, FromLevel = h.FromLevel, ToLevel = h.ToLevel,
                    Result = h.Result, GoldCost = h.GoldCost, CreatedAt = h.CreatedAt,
                }).ToListAsync();
        });

        admin.MapGet("/pk-records", async (long? characterId, int? limit, GameDbContext db) =>
        {
            var query = db.PkRecords.AsNoTracking();
            if (characterId.HasValue)
                query = query.Where(r => (r.KillerKind == EntityKind.Character && r.KillerId == characterId)
                                         || (r.VictimKind == EntityKind.Character && r.VictimId == characterId));
            return await query.OrderByDescending(r => r.Id).Take(Math.Clamp(limit ?? 100, 1, 1000))
                .Select(r => new PkRecordDto
                {
                    KillerKind = r.KillerKind, KillerName = r.KillerName, VictimKind = r.VictimKind, VictimName = r.VictimName,
                    VictimWasInnocent = r.VictimWasInnocent, AlignmentDelta = r.AlignmentDelta, CreatedAt = r.CreatedAt,
                }).ToListAsync();
        });
    }

    private static void MapCharacterChanges(RouteGroupBuilder admin)
    {
        admin.MapPatch("/characters/{id:long}", async (long id, AdminCharacterUpdate req, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            if (req.Gold < 0) throw GameErrors.Validation("골드는 0 이상이어야 합니다.");
            if (req.Level is < 1 || req.Level > ExpTable.MaxLevel) throw GameErrors.Validation($"레벨은 1~{ExpTable.MaxLevel} 사이여야 합니다.");
            if (req.Exp < 0) throw GameErrors.Validation("경험치는 0 이상이어야 합니다.");
            if (req.X.HasValue != req.Z.HasValue) throw GameErrors.Validation("위치는 x, z 를 함께 보내야 합니다.");
            if (req.X.HasValue) GameOps.RequireFinite(req.X.Value, req.Z!.Value);

            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await LockCharacterAsync(db, id);
                if (req.Gold.HasValue && req.Gold.Value != character.Gold)
                    db.ChangeGold(character, req.Gold.Value - character.Gold, LedgerReasons.AdminAdjust, "Admin", null, now);
                if (req.Level.HasValue)
                {
                    character.Level = req.Level.Value;
                    character.Exp = 0;
                }
                if (req.Exp.HasValue) character.Exp = Math.Min(req.Exp.Value, Math.Max(0, ExpTable.ExpToNext(character.Level) - 1));
                if (req.Alignment.HasValue)
                {
                    var next = AlignmentRules.Clamp(config.Alignment, req.Alignment.Value);
                    db.RecordAlignment(EntityKind.Character, character.Id, next - character.Alignment, next, "ADMIN", now);
                    character.Alignment = next;
                }
                if (req.ClearPurple) character.PurpleUntil = null;
                if (req.X.HasValue)
                {
                    character.PosX = Math.Clamp(req.X.Value, 0, ZoneMap.WorldSize);
                    character.PosZ = Math.Clamp(req.Z!.Value, 0, ZoneMap.WorldSize);
                }
            });
            return await db.CharacterDtoAsync(character, now);
        });

        admin.MapPost("/characters/{id:long}/items", async (long id, GmItemRequest req, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            var template = GmEndpoints.ValidateItem(req, config);
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await LockCharacterAsync(db, id);
                await GmEndpoints.GiveItemAsync(db, character, template, req, now);
            });
            return await db.InventoryDtoAsync(character);
        });

        admin.MapDelete("/characters/{id:long}/items/{itemId:long}", async (long id, long itemId, GameDbContext db) =>
        {
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await LockCharacterAsync(db, id);
                var item = await db.Items.SingleOrDefaultAsync(i => i.Id == itemId && i.OwnerKind == OwnerKind.Character && i.OwnerId == id)
                           ?? throw GameErrors.NotFound("아이템");
                db.Items.Remove(item);
            });
            return await db.InventoryDtoAsync(character);
        });

        // 새로 만든 캐릭터 상태(레벨 1, 시작 골드·장비, 성향 0, 마을)로 되돌린다. 원장 기록은 남긴다.
        admin.MapPost("/characters/{id:long}/reset", async (long id, GameDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await LockCharacterAsync(db, id);
                await db.Items.Where(i => i.OwnerKind == OwnerKind.Character && i.OwnerId == id).ExecuteDeleteAsync();
                await db.SpawnCooldowns.Where(s => s.CharacterId == id).ExecuteDeleteAsync();
                if (character.Gold != ItemCatalog.StarterGold)
                    db.ChangeGold(character, ItemCatalog.StarterGold - character.Gold, LedgerReasons.AdminReset, "Admin", null, now);
                if (character.Alignment != 0) db.RecordAlignment(EntityKind.Character, id, -character.Alignment, 0, "ADMIN_RESET", now);
                character.Level = 1;
                character.Exp = 0;
                character.Alignment = 0;
                character.PurpleUntil = null;
                character.PkKills = 0;
                character.PkDeaths = 0;
                character.PosX = ZoneMap.RespawnX;
                character.PosY = 0;
                character.PosZ = ZoneMap.RespawnZ;
                db.AddStarterItems(character, now);
            });
            return await db.CharacterDtoAsync(character, now);
        });
    }

    private static void MapTestControls(RouteGroupBuilder admin)
    {
        admin.MapGet("/config", (RuntimeGameConfig config, ForcedOutcomes forced) => new AdminConfigDto
        {
            Enhance = config.Enhance, Drop = config.Drop, Forced = ToDto(forced),
        });

        admin.MapPut("/config/enhance-rates", (GmEnhanceRateRequest req, RuntimeGameConfig config) => GmEndpoints.ApplyEnhanceRates(req, config));
        admin.MapPut("/config/drop-rates", (GmDropRateRequest req, RuntimeGameConfig config) => GmEndpoints.ApplyDropRates(req, config));

        admin.MapGet("/forced", (ForcedOutcomes forced) => ToDto(forced));
        admin.MapPost("/forced", (AdminForceRequest req, ForcedOutcomes forced) =>
        {
            if (req.Enhance?.Any(r => !Enum.IsDefined(r)) == true) throw GameErrors.Validation("강화 결과 값이 올바르지 않습니다.");
            forced.Enqueue(req.Enhance, req.BotLoot, req.DeathDrop);
            return ToDto(forced);
        });
        admin.MapDelete("/forced", (ForcedOutcomes forced) =>
        {
            forced.Clear();
            return ToDto(forced);
        });

        admin.MapPost("/cleanup", async (AdminCleanupRequest req, GameDbContext db) =>
        {
            var prefix = req.UsernamePrefix?.Trim() ?? "";
            if (prefix.Length < 3) throw GameErrors.Validation("usernamePrefix 는 3자 이상이어야 합니다.");
            var response = new AdminCleanupResponse();
            await db.InTransactionAsync(async () =>
            {
                var accountIds = await db.Accounts.Where(a => a.Username.StartsWith(prefix)).Select(a => a.Id).ToListAsync();
                var characterIds = await db.Characters.Where(c => accountIds.Contains(c.AccountId)).Select(c => c.Id).ToListAsync();
                // 아이템·쿨다운·이력은 캐릭터 외래 키가 없어서 먼저 지운다. 원장·리프레시 토큰은 계정 삭제로 함께 지워진다.
                await db.Items.Where(i => i.OwnerKind == OwnerKind.Character && characterIds.Contains(i.OwnerId)).ExecuteDeleteAsync();
                await db.SpawnCooldowns.Where(s => characterIds.Contains(s.CharacterId)).ExecuteDeleteAsync();
                await db.EnhanceHistories.Where(h => characterIds.Contains(h.CharacterId)).ExecuteDeleteAsync();
                await db.AlignmentHistories.Where(h => h.EntityKind == EntityKind.Character && characterIds.Contains(h.EntityId)).ExecuteDeleteAsync();
                await db.PkRecords.Where(r => (r.KillerKind == EntityKind.Character && characterIds.Contains(r.KillerId))
                                              || (r.VictimKind == EntityKind.Character && characterIds.Contains(r.VictimId))).ExecuteDeleteAsync();
                response.CharactersDeleted = characterIds.Count;
                response.AccountsDeleted = await db.Accounts.Where(a => accountIds.Contains(a.Id)).ExecuteDeleteAsync();
            });
            return response;
        });

        admin.MapPost("/world/reset", async (AdminWorldResetRequest req, GameDbContext db, BotService bots) =>
        {
            var response = new AdminWorldResetResponse();
            await db.InTransactionAsync(async () =>
            {
                var ground = db.GroundItems.AsQueryable();
                if (req.Zone.HasValue) ground = ground.Where(g => g.Zone == req.Zone.Value);
                var itemIds = await ground.Select(g => g.ItemId).ToListAsync();
                // 아이템 행을 지우면 바닥 행도 함께 지워진다.
                response.GroundItemsDeleted = await db.Items.Where(i => itemIds.Contains(i.Id)).ExecuteDeleteAsync();
                response.BotCount = await bots.ResetAsync(req.Zone);
            });
            return response;
        });
    }

    private static void MapSpawns(RouteGroupBuilder admin)
    {
        admin.MapGet("/spawns", async (GameDbContext db) =>
            (await db.MonsterSpawns.AsNoTracking().OrderBy(s => s.Id).ToListAsync()).Select(SpawnService.ToDto).ToList());

        // 있으면 고치고 없으면 만든다. 클라이언트는 다음 월드 입장 때 새 표를 받는다.
        admin.MapPut("/spawns/{id}", async (string id, AdminSpawnRequest req, GameDbContext db) =>
        {
            id = id.Trim();
            if (id.Length is < 1 or > 16) throw GameErrors.Validation("스폰 ID 는 1~16자여야 합니다.");
            if (!Enum.IsDefined(req.Zone) || !Enum.IsDefined(req.Monster)) throw GameErrors.Validation("구역 또는 몬스터 값이 올바르지 않습니다.");
            GameOps.RequireFinite(req.X, req.Z);
            if (req.X < 0 || req.X > ZoneMap.WorldSize || req.Z < 0 || req.Z > ZoneMap.WorldSize)
                throw GameErrors.Validation($"좌표는 0~{ZoneMap.WorldSize} 사이여야 합니다.");
            if (ZoneMap.IsSafeZone(ZoneMap.GetZone(req.X, req.Z))) throw GameErrors.Validation("마을(안전지대)에는 몬스터를 둘 수 없습니다.");

            var spawn = await db.MonsterSpawns.FindAsync(id);
            if (spawn == null) db.MonsterSpawns.Add(spawn = new MonsterSpawn { Id = id });
            spawn.Zone = req.Zone;
            spawn.Monster = req.Monster;
            spawn.X = req.X;
            spawn.Z = req.Z;
            await db.SaveChangesAsync();
            return SpawnService.ToDto(spawn);
        });

        admin.MapDelete("/spawns/{id}", async (string id, GameDbContext db) =>
        {
            var deleted = await db.MonsterSpawns.Where(s => s.Id == id).ExecuteDeleteAsync();
            if (deleted == 0) throw GameErrors.NotFound("스폰");
            return new AdminCountResponse { Count = await db.MonsterSpawns.CountAsync() };
        });

        admin.MapPost("/spawns/reset", async (GameDbContext db) =>
        {
            await db.InTransactionAsync(() => SpawnService.ResetAsync(db));
            return new AdminCountResponse { Count = await db.MonsterSpawns.CountAsync() };
        });
    }

    private static async Task<Character> FindCharacterAsync(GameDbContext db, long id) =>
        await db.Characters.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id) ?? throw GameErrors.NotFound("캐릭터");

    private static async Task<Character> LockCharacterAsync(GameDbContext db, long id) =>
        await db.LockCharacterAsync(id) ?? throw GameErrors.NotFound("캐릭터");

    private static AdminCharacterDto ToAdmin(Character c, string username) => new()
    {
        Id = c.Id, AccountId = c.AccountId, Username = username, Name = c.Name, ClassType = c.ClassType, Level = c.Level,
        Exp = c.Exp, Gold = c.Gold, X = c.PosX, Z = c.PosZ, Zone = ZoneMap.GetZone(c.PosX, c.PosZ), Alignment = c.Alignment,
        PurpleUntil = c.PurpleUntil, PkKills = c.PkKills, PkDeaths = c.PkDeaths, CreatedAt = c.CreatedAt, Deleted = c.DeletedAt != null,
    };

    private static AdminForcedDto ToDto(ForcedOutcomes forced)
    {
        var (enhance, botLoot, deathDrop) = forced.Snapshot();
        return new AdminForcedDto { Enhance = enhance.ToList(), BotLoot = botLoot.ToList(), DeathDrop = deathDrop.ToList() };
    }
}
