// 상점 목록·구매·판매, 장비 강화, 재화 원장 조회 API
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Endpoints;

public static class ShopEndpoints
{
    public static void MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/shop").WithTags("Shop").RequireAuthorization();

        shop.MapGet("/items", async (HttpContext http, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            var character = await db.GetOwnedCharacterAsync(http.CharacterId(), http.AccountId());
            var state = AlignmentRules.GetState(character.Alignment, character.PurpleUntil, clock.GetUtcNow().UtcDateTime);
            return new ShopItemsResponse
            {
                PriceMultiplier = ShopRules.PricePercent(config.Shop, state) / 100.0,
                Items = ItemCatalog.ShopListed()
                    .Select(t => new ShopItemDto { TemplateId = t.Id, Price = ShopRules.BuyPrice(config.Shop, t, 1, state) })
                    .ToList(),
            };
        });

        shop.MapPost("/buy", async (BuyRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            if (!ItemCatalog.TryGet(req.TemplateId, out var template) || !template.ShopListed)
                throw GameErrors.NotFound("판매 상품");
            if (req.Quantity < 1 || req.Quantity > (template.Stackable ? ItemCatalog.MaxStack : InventoryRules.Capacity))
                throw GameErrors.Validation("구매 수량이 올바르지 않습니다.");

            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var state = AlignmentRules.GetState(character.Alignment, character.PurpleUntil, now);
                var price = ShopRules.BuyPrice(config.Shop, template, req.Quantity, state);
                if (character.Gold < price) throw GameErrors.Conflict(ErrorCodes.InsufficientGold);

                var items = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                var plan = GameOps.Plan(items, template.Id, req.Quantity);
                if (!plan.Fits) throw GameErrors.Conflict(ErrorCodes.InventoryFull);

                db.ChangeGold(character, -price, LedgerReasons.ShopBuy, "Item", $"{template.Id}x{req.Quantity}", now);
                db.AddNewItems(OwnerKind.Character, character.Id, items, plan, template.Id, 0, now);
            });
            return await db.InventoryDtoAsync(character);
        });

        shop.MapPost("/sell", async (SellRequest req, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var item = await db.Items.SingleOrDefaultAsync(i =>
                    i.Id == req.ItemId && i.OwnerKind == OwnerKind.Character && i.OwnerId == character.Id)
                    ?? throw GameErrors.NotFound("아이템");
                if (item.EquippedSlot != null) throw GameErrors.Conflict(ErrorCodes.ItemEquipped);
                if (req.Quantity < 1 || req.Quantity > item.Quantity) throw GameErrors.Conflict(ErrorCodes.NotEnoughQuantity);

                var template = ItemCatalog.Get(item.TemplateId);
                var gain = ItemRules.SellPrice(template, item.EnhanceLevel) * req.Quantity;
                if (gain <= 0) throw GameErrors.Conflict(ErrorCodes.NotSellable);

                if (req.Quantity == item.Quantity) db.Items.Remove(item);
                else item.Quantity -= req.Quantity;
                db.ChangeGold(character, gain, LedgerReasons.ShopSell, "Item", item.Id.ToString(), now);
            });
            return await db.InventoryDtoAsync(character);
        });

        app.MapPost("/enhance", async (EnhanceRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config,
            IRandomSource random, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var enhance = config.Enhance;
            Character character = null!;
            EnhanceResponse response = null!;
            Item target = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var items = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                target = items.FirstOrDefault(i => i.Id == req.ItemId) ?? throw GameErrors.NotFound("강화할 아이템");
                var scroll = items.FirstOrDefault(i => i.Id == req.ScrollId) ?? throw GameErrors.NotFound("주문서");

                var targetTemplate = ItemCatalog.Get(target.TemplateId);
                var error = EnhanceRules.Validate(enhance, targetTemplate, target.EnhanceLevel, ItemCatalog.Get(scroll.TemplateId));
                if (error != null) throw GameErrors.Conflict(error);

                var cost = EnhanceRules.GoldCost(enhance, target.EnhanceLevel);
                if (character.Gold < cost) throw GameErrors.Conflict(ErrorCodes.InsufficientGold);

                if (scroll.Quantity > 1) scroll.Quantity -= 1;
                else db.Items.Remove(scroll);
                db.ChangeGold(character, -cost, LedgerReasons.Enhance, "Item", target.Id.ToString(), now);

                var from = target.EnhanceLevel;
                var result = EnhanceRules.Roll(enhance, from, random);
                if (result == EnhanceResult.Success) target.EnhanceLevel += 1;
                if (result == EnhanceResult.Destroy) db.Items.Remove(target);

                db.EnhanceHistories.Add(new EnhanceHistory
                {
                    CharacterId = character.Id, ItemId = target.Id, TemplateId = target.TemplateId, FromLevel = from,
                    ToLevel = result == EnhanceResult.Success ? from + 1 : from, Result = result, GoldCost = cost, CreatedAt = now,
                });
                response = new EnhanceResponse { Result = result, FromLevel = from, ToLevel = target.EnhanceLevel, GoldSpent = cost };
            });

            response.Item = response.Result == EnhanceResult.Destroy ? null : Map.Item(target);
            response.Inventory = await db.InventoryDtoAsync(character);
            return response;
        }).WithTags("Enhance").RequireAuthorization();

        app.MapGet("/currency/ledger", async (HttpContext http, GameDbContext db, int? limit) =>
        {
            var character = await db.GetOwnedCharacterAsync(http.CharacterId(), http.AccountId());
            var take = Math.Clamp(limit ?? 50, 1, 200);
            var entries = await db.LedgerEntries.AsNoTracking()
                .Where(e => e.CharacterId == character.Id)
                .OrderByDescending(e => e.Id).Take(take).ToListAsync();
            return entries.Select(e => new LedgerEntryDto
            {
                Id = e.Id, Reason = e.Reason, Delta = e.Delta, BalanceAfter = e.BalanceAfter,
                RelatedType = e.RelatedType, RelatedId = e.RelatedId, CreatedAt = e.CreatedAt,
            }).ToList();
        }).WithTags("Currency").RequireAuthorization();
    }
}
