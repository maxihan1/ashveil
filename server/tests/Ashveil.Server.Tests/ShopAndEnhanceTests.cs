// 상점(구매·판매·인벤토리 가득·악명 할증·동시 구매)과 강화(성공·실패·파괴·주문서) 통합 테스트
using System.Net;
using System.Net.Http.Json;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

public class ShopAndEnhanceTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task Buy_MergesStack_DeductsGold_WritesLedger()
    {
        var (client, character) = await server.NewPlayerAsync();
        var inventory = await client.PostAsync<InventoryDto>("/shop/buy", new BuyRequest { TemplateId = ItemCatalog.PotionHpSmall, Quantity = 5 });

        Assert.Equal(350, inventory.Gold);
        Assert.Equal(3, inventory.Used); // 기존 물약 스택에 합쳐진다
        Assert.Equal(15, inventory.Items.Single(i => i.TemplateId == ItemCatalog.PotionHpSmall).Quantity);

        var last = (await client.LedgerAsync()).First();
        Assert.Equal(LedgerReasons.ShopBuy, last.Reason);
        Assert.Equal(-150, last.Delta);
        Assert.Equal(350, last.BalanceAfter);
        Assert.Equal(350, (await client.MeAsync(character.Id)).Gold);
    }

    [Fact]
    public async Task Buy_WithoutEnoughGold_IsRejectedWithoutChanges()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.PostErrorAsync("/shop/buy", new BuyRequest { TemplateId = "armor_chain", Quantity = 1 },
            HttpStatusCode.Conflict, ErrorCodes.InsufficientGold);
        Assert.Equal(500, (await client.MeAsync(character.Id)).Gold);
        Assert.Single(await client.LedgerAsync());
    }

    [Fact]
    public async Task Buy_WithFullInventory_IsRejectedWithoutChargingGold()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.GmItemAsync("gloves_leather", InventoryRules.Capacity - 3);
        Assert.Equal(InventoryRules.Capacity, (await client.InventoryAsync(character.Id)).Used);

        await client.PostErrorAsync("/shop/buy", new BuyRequest { TemplateId = "helm_leather", Quantity = 1 },
            HttpStatusCode.Conflict, ErrorCodes.InventoryFull);
        Assert.Equal(500, (await client.MeAsync(character.Id)).Gold);

        // 겹치는 아이템은 기존 스택에 들어가므로 가득 차도 살 수 있다.
        var inventory = await client.PostAsync<InventoryDto>("/shop/buy", new BuyRequest { TemplateId = ItemCatalog.PotionHpSmall, Quantity = 1 });
        Assert.Equal(470, inventory.Gold);
    }

    [Fact]
    public async Task Sell_PaysSellPrice_ButEquippedItemsCannotBeSold()
    {
        var (client, character) = await server.NewPlayerAsync();
        var inventory = await client.InventoryAsync(character.Id);
        var weapon = inventory.Items.Single(i => i.EquippedSlot == EquipSlot.Weapon);
        var potions = inventory.Items.Single(i => i.TemplateId == ItemCatalog.PotionHpSmall);

        await client.PostErrorAsync("/shop/sell", new SellRequest { ItemId = weapon.Id, Quantity = 1 }, HttpStatusCode.Conflict, ErrorCodes.ItemEquipped);
        await client.PostErrorAsync("/shop/sell", new SellRequest { ItemId = potions.Id, Quantity = 11 }, HttpStatusCode.Conflict, ErrorCodes.NotEnoughQuantity);

        var after = await client.PostAsync<InventoryDto>("/shop/sell", new SellRequest { ItemId = potions.Id, Quantity = 4 });
        Assert.Equal(540, after.Gold);
        Assert.Equal(6, after.Items.Single(i => i.Id == potions.Id).Quantity);
        Assert.Equal(LedgerReasons.ShopSell, (await client.LedgerAsync()).First().Reason);
    }

    [Fact]
    public async Task ChaoticCharacter_PaysTwentyPercentMore()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.GmAlignmentAsync(-50);

        var shop = await client.GetAsync<ShopItemsResponse>("/shop/items");
        Assert.Equal(1.2, shop.PriceMultiplier);
        Assert.Equal(180, shop.Items.Single(i => i.TemplateId == "helm_leather").Price);

        var inventory = await client.PostAsync<InventoryDto>("/shop/buy", new BuyRequest { TemplateId = "helm_leather", Quantity = 1 });
        Assert.Equal(320, inventory.Gold);
    }

    [Fact]
    public async Task ConcurrentPurchases_NeverOverspend()
    {
        var (client, character) = await server.NewPlayerAsync();
        // 골드 500, 가죽 투구 150 → 최대 3개
        var tasks = Enumerable.Range(0, 8).Select(_ =>
            client.Http.PostAsJsonAsync("/shop/buy", new BuyRequest { TemplateId = "helm_leather", Quantity = 1 }, GameClient.Json));
        var responses = await Task.WhenAll(tasks);

        Assert.Equal(3, responses.Count(r => r.IsSuccessStatusCode));
        Assert.All(responses.Where(r => !r.IsSuccessStatusCode), r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));

        var me = await client.MeAsync(character.Id);
        Assert.Equal(50, me.Gold);
        var ledger = await client.LedgerAsync();
        Assert.Equal(me.Gold, ledger.Sum(e => e.Delta));
        Assert.Equal(me.Gold, ledger.First().BalanceAfter);
        Assert.Equal(3, (await client.InventoryAsync(character.Id)).Items.Count(i => i.TemplateId == "helm_leather"));
    }

    [Fact]
    public async Task Enhance_SafeZoneAlwaysSucceeds_AndChargesGoldAndScroll()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.GmGoldAsync(1000);
        var inventory = await client.GmItemAsync(ItemCatalog.ScrollWeapon, 3);
        var weapon = inventory.Items.Single(i => i.EquippedSlot == EquipSlot.Weapon);
        var scroll = inventory.Items.Single(i => i.TemplateId == ItemCatalog.ScrollWeapon);

        EnhanceResponse result = null!;
        for (var i = 0; i < 3; i++)
        {
            server.Random.Enqueue(0.9999); // 안전 구간은 어떤 값이 나와도 성공
            result = await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = weapon.Id, ScrollId = scroll.Id });
            Assert.Equal(EnhanceResult.Success, result.Result);
        }
        Assert.Equal(3, result.Item!.EnhanceLevel);
        Assert.Equal(1500 - 100 - 200 - 300, result.Inventory.Gold);
        Assert.DoesNotContain(result.Inventory.Items, i => i.TemplateId == ItemCatalog.ScrollWeapon);
        Assert.Equal(3, (await client.LedgerAsync()).Count(e => e.Reason == LedgerReasons.Enhance));
        Assert.Equal(12 + 8 + 6, (await client.MeAsync(character.Id)).Stats.Attack);
    }

    [Fact]
    public async Task Enhance_FailKeepsLevel_DestroyDeletesItem()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.GmGoldAsync(5000);
        await client.GmItemAsync("helm_iron", 1, enhance: 3);
        var inventory = await client.GmItemAsync(ItemCatalog.ScrollArmor, 2);
        var helm = inventory.Items.Single(i => i.TemplateId == "helm_iron");
        var scroll = inventory.Items.Single(i => i.TemplateId == ItemCatalog.ScrollArmor);

        try
        {
            await client.PostAsync<EnhanceConfig>("/gm/enhance-rate", new GmEnhanceRateRequest
            {
                Rates = [new EnhanceRate { TargetLevel = 4, Success = 0, Fail = 1, Destroy = 0 }],
            });
            var fail = await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id });
            Assert.Equal(EnhanceResult.Fail, fail.Result);
            Assert.Equal(3, fail.Item!.EnhanceLevel);

            await client.PostAsync<EnhanceConfig>("/gm/enhance-rate", new GmEnhanceRateRequest
            {
                Rates = [new EnhanceRate { TargetLevel = 4, Success = 0, Fail = 0, Destroy = 1 }],
            });
            var destroy = await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id });
            Assert.Equal(EnhanceResult.Destroy, destroy.Result);
            Assert.Null(destroy.Item);
            Assert.DoesNotContain(destroy.Inventory.Items, i => i.Id == helm.Id);
            Assert.Equal(5500 - 400 - 400, destroy.Inventory.Gold);
        }
        finally
        {
            await client.PostAsync<EnhanceConfig>("/gm/enhance-rate", new GmEnhanceRateRequest { Reset = true });
        }
    }

    [Fact]
    public async Task Enhance_WithWrongScroll_IsRejected()
    {
        var (client, character) = await server.NewPlayerAsync();
        var inventory = await client.GmItemAsync(ItemCatalog.ScrollArmor, 1);
        var weapon = inventory.Items.Single(i => i.EquippedSlot == EquipSlot.Weapon);
        var scroll = inventory.Items.Single(i => i.TemplateId == ItemCatalog.ScrollArmor);
        await client.PostErrorAsync("/enhance", new EnhanceRequest { ItemId = weapon.Id, ScrollId = scroll.Id },
            HttpStatusCode.Conflict, ErrorCodes.ScrollMismatch);
        Assert.Equal(500, (await client.MeAsync(character.Id)).Gold);
    }
}
