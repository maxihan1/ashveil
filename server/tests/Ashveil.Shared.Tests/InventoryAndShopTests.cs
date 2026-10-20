// 인벤토리 적재 계획, 아이템 카탈로그, 상점 가격 규칙 테스트
using Ashveil.Shared;
using Xunit;

namespace Ashveil.Shared.Tests;

public class InventoryAndShopTests
{
    private static List<StackView> Stacks(params (long id, string template, int qty)[] items) =>
        items.Select(i => new StackView(i.id, i.template, i.qty)).ToList();

    [Fact]
    public void PlanAdd_MergesIntoExistingStackFirst()
    {
        var plan = InventoryRules.PlanAdd(Stacks((1, "potion_hp_s", 10)), "potion_hp_s", 5);
        Assert.True(plan.Fits);
        Assert.Single(plan.Merges);
        Assert.Equal((1L, 5), plan.Merges[0]);
        Assert.Empty(plan.NewStacks);
    }

    [Fact]
    public void PlanAdd_OverflowGoesToNewStack()
    {
        var plan = InventoryRules.PlanAdd(Stacks((1, "potion_hp_s", 995)), "potion_hp_s", 10);
        Assert.True(plan.Fits);
        Assert.Equal((1L, 4), plan.Merges[0]);
        Assert.Equal(new[] { 6 }, plan.NewStacks);
    }

    [Fact]
    public void PlanAdd_NonStackable_TakesOneSlotEach()
    {
        var plan = InventoryRules.PlanAdd(Stacks(), "sword_short", 3);
        Assert.True(plan.Fits);
        Assert.Equal(new[] { 1, 1, 1 }, plan.NewStacks);
    }

    [Fact]
    public void PlanAdd_FullInventory_DoesNotFit_ButStackMergeStillWorks()
    {
        var full = Enumerable.Range(1, InventoryRules.Capacity)
            .Select(i => (id: (long)i, template: i == 1 ? "potion_hp_s" : "sword_short", qty: 1)).ToArray();

        Assert.False(InventoryRules.PlanAdd(Stacks(full), "sword_short", 1).Fits);
        Assert.True(InventoryRules.PlanAdd(Stacks(full), "potion_hp_s", 3).Fits);
        Assert.False(InventoryRules.PlanAdd(Stacks(full), "potion_mp_s", 1).Fits);
    }

    [Fact]
    public void Catalog_EquipmentHasSlot_ConsumablesStack()
    {
        Assert.Equal(EquipSlot.Weapon, ItemCatalog.Get("bow_long").Slot);
        Assert.Equal(EquipSlot.Boots, ItemCatalog.Get("boots_iron").Slot);
        Assert.Null(ItemCatalog.Get("potion_hp_s").Slot);
        Assert.True(ItemCatalog.Get("scroll_weapon").Stackable);
        Assert.False(ItemCatalog.Get("sword_short").Stackable);
        Assert.False(ItemCatalog.TryGet("nope", out _));
    }

    [Fact]
    public void StarterWeapon_DependsOnClass()
    {
        Assert.Equal("sword_short", ItemCatalog.StarterWeapon(ClassType.Warrior));
        Assert.Equal("bow_short", ItemCatalog.StarterWeapon(ClassType.Archer));
        Assert.Equal("staff_oak", ItemCatalog.StarterWeapon(ClassType.Mage));
    }

    [Fact]
    public void BuyPrice_ChaoticPaysMarkup_RoundedUp()
    {
        var config = new ShopConfig { ChaoticPricePercent = 120 };
        Assert.Equal(300, ShopRules.BuyPrice(config, ItemCatalog.Get("sword_short"), 1, AlignmentState.Innocent));
        Assert.Equal(360, ShopRules.BuyPrice(config, ItemCatalog.Get("sword_short"), 1, AlignmentState.Chaotic));
        Assert.Equal(36, ShopRules.BuyPrice(config, ItemCatalog.Get("potion_hp_s"), 1, AlignmentState.Chaotic));
        Assert.Equal(300, ShopRules.BuyPrice(config, ItemCatalog.Get("potion_hp_s"), 10, AlignmentState.Purple));
    }

    [Fact]
    public void ShopListing_ExcludesLootOnlyItems()
    {
        var listed = ItemCatalog.ShopListed().Select(t => t.Id).ToList();
        Assert.Contains("potion_hp_s", listed);
        Assert.DoesNotContain("axe_battle", listed);
        Assert.DoesNotContain("armor_plate", listed);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 150)]
    [InlineData(3, 250)]
    public void SellPrice_GrowsWithEnhanceLevel(int enhance, long expected)
    {
        Assert.Equal(expected, ItemRules.SellPrice(ItemCatalog.Get("sword_short"), enhance));
    }

    [Theory]
    [InlineData("용사", true)]
    [InlineData("Hero01", true)]
    [InlineData("a", false)]
    [InlineData("이름이너무길어서열세글자다", false)]
    [InlineData("bad name", false)]
    public void CharacterNameRule(string name, bool valid)
    {
        Assert.Equal(valid, NameRules.IsValidCharacterName(name));
    }

    [Theory]
    [InlineData("tester01", true)]
    [InlineData("abc", false)]
    [InlineData("Upper", false)]
    public void UsernameRule(string username, bool valid)
    {
        Assert.Equal(valid, NameRules.IsValidUsername(username));
    }
}
