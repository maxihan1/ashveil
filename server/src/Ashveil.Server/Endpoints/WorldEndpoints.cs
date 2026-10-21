// 몬스터 스폰 표, 바닥 아이템 조회·줍기(플레이어·봇 공용)와 구역별 봇 목록 API
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Endpoints;

public static class WorldEndpoints
{
    public static void MapWorldEndpoints(this IEndpointRouteBuilder app)
    {
        var world = app.MapGroup("/world").WithTags("World").RequireAuthorization();

        world.MapGet("/spawns", async (GameDbContext db) =>
        {
            var list = await db.MonsterSpawns.AsNoTracking().OrderBy(s => s.Id).ToListAsync();
            return list.Select(SpawnService.ToDto).ToList();
        });

        world.MapGet("/ground-items", async (ZoneId? zone, GameDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            // 만료된 바닥 아이템은 읽을 때 정리한다(아이템 행을 지우면 바닥 행도 같이 지워진다).
            await db.Items.Where(i => i.OwnerKind == OwnerKind.Ground && db.GroundItems.Any(g => g.ItemId == i.Id && g.ExpiresAt <= now))
                .ExecuteDeleteAsync();
            var query = db.GroundItems.AsNoTracking().Include(g => g.Item).Where(g => g.ExpiresAt > now);
            if (zone.HasValue) query = query.Where(g => g.Zone == zone.Value);
            var list = await query.OrderBy(g => g.ItemId).ToListAsync();
            return list.Select(Map.Ground).ToList();
        });

        world.MapPost("/ground-items/{id:long}/pickup", async (long id, PickupRequest req, HttpContext http, GameDbContext db,
            RuntimeGameConfig config, TimeProvider clock) =>
        {
            GameOps.RequireFinite(req.X, req.Z);
            var now = clock.GetUtcNow().UtcDateTime;
            var response = new PickupResponse();
            Character character = null!;
            Item landed = null!;
            await db.InTransactionAsync(async () =>
            {
                // 잠금 순서: 캐릭터 → 봇 → 바닥 아이템
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                Bot? bot = null;
                if (req.BotId.HasValue)
                {
                    bot = await db.LockBotAsync(req.BotId.Value) ?? throw GameErrors.NotFound("봇");
                    if (bot.IsDead) throw GameErrors.Conflict(ErrorCodes.BotDead);
                }

                var ground = await db.LockGroundItemAsync(id) ?? throw GameErrors.NotFound("바닥 아이템");
                if (ground.ExpiresAt <= now) throw GameErrors.Conflict(ErrorCodes.ItemExpired); // 정리는 목록 조회가 맡는다

                db.GroundItems.Remove(ground);
                if (bot == null)
                {
                    var dx = req.X - ground.X;
                    var dz = req.Z - ground.Z;
                    if (dx * dx + dz * dz > config.Combat.PickupRange * config.Combat.PickupRange) throw GameErrors.Conflict(ErrorCodes.OutOfRange);
                    var items = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                    var plan = GameOps.Plan(items, ground.Item.TemplateId, ground.Item.Quantity);
                    if (!plan.Fits) throw GameErrors.Conflict(ErrorCodes.InventoryFull);
                    landed = db.MoveItem(ground.Item, OwnerKind.Character, character.Id, items, plan);
                    response.PickedByKind = EntityKind.Character;
                    response.PickedById = character.Id;
                }
                else
                {
                    if (bot.Zone != ground.Zone) throw GameErrors.Conflict(ErrorCodes.InvalidTarget);
                    var count = await db.Items.CountAsync(i => i.OwnerKind == OwnerKind.Bot && i.OwnerId == bot.Id);
                    if (count >= config.Bots.InventoryCapacity) throw GameErrors.Conflict(ErrorCodes.InventoryFull);
                    var item = ground.Item;
                    item.OwnerKind = OwnerKind.Bot;
                    item.OwnerId = bot.Id;
                    item.EquippedSlot = null;
                    landed = item;
                    response.PickedByKind = EntityKind.Bot;
                    response.PickedById = bot.Id;
                }
            });

            response.Item = Map.Item(landed);
            if (response.PickedByKind == EntityKind.Character) response.Inventory = await db.InventoryDtoAsync(character);
            return response;
        });

        app.MapGet("/bots", async (ZoneId? zone, GameDbContext db, BotService bots, TimeProvider clock) =>
        {
            await bots.ReviveDueAsync();
            var now = clock.GetUtcNow().UtcDateTime;
            var query = db.Bots.AsNoTracking();
            if (zone.HasValue) query = query.Where(b => b.Zone == zone.Value);
            var list = await query.OrderBy(b => b.Id).ToListAsync();
            var ids = list.Select(b => b.Id).ToList();
            var items = await db.Items.AsNoTracking()
                .Where(i => i.OwnerKind == OwnerKind.Bot && ids.Contains(i.OwnerId)).ToListAsync();
            var byOwner = items.ToLookup(i => i.OwnerId);
            return list.Select(b => Map.Bot(b, byOwner[b.Id], now)).ToList();
        }).WithTags("Bots").RequireAuthorization();
    }
}
