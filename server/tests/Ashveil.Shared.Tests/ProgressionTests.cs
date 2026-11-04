// 경험치 표와 스탯 계산 규칙 테스트
using Ashveil.Shared;
using Xunit;

namespace Ashveil.Shared.Tests;

public class ProgressionTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(2, 300)]
    [InlineData(10, 5500)]
    public void ExpToNext_FollowsTriangularFormula(int level, long expected)
    {
        Assert.Equal(expected, ExpTable.ExpToNext(level));
    }

    [Fact]
    public void ApplyExp_CarriesOverflowAcrossMultipleLevels()
    {
        // Lv1(0) + 450 → Lv2 필요 100 → 350 남음 → Lv3 필요 300 → 50 남음
        var result = ExpTable.ApplyExp(1, 0, 450);
        Assert.Equal(3, result.Level);
        Assert.Equal(50, result.Exp);
        Assert.True(result.LeveledUp);
    }

    [Fact]
    public void ApplyExp_StopsAtMaxLevel()
    {
        var result = ExpTable.ApplyExp(ExpTable.MaxLevel, 0, 1_000_000_000);
        Assert.Equal(ExpTable.MaxLevel, result.Level);
        Assert.Equal(0, result.Exp);
        Assert.False(result.LeveledUp);
    }

    [Fact]
    public void DeathExpLoss_IsTenPercentOfRequirement_CappedAtCurrentExp()
    {
        Assert.Equal(30, ExpTable.DeathExpLoss(2, 250)); // 300의 10%
        Assert.Equal(5, ExpTable.DeathExpLoss(2, 5));    // 레벨은 내리지 않는다
    }

    [Fact]
    public void Stats_IncludeClassGrowth()
    {
        var lv1 = StatRules.Compute(ClassType.Warrior, 1, Array.Empty<EquippedItem>());
        Assert.Equal(new Stats(120, 30, 12, 6), lv1);

        var lv5 = StatRules.Compute(ClassType.Warrior, 5, Array.Empty<EquippedItem>());
        Assert.Equal(new Stats(120 + 4 * 12, 30 + 4 * 2, 12 + 4 * 2, 6 + 4 * 1), lv5);
    }

    [Fact]
    public void Stats_IncludeEquipmentAndEnhanceBonus()
    {
        var equipped = new[]
        {
            new EquippedItem("sword_short", 2),   // 공격력 8 + 2*2
            new EquippedItem("armor_leather", 3), // 방어력 4 + 3
        };
        var stats = StatRules.Compute(ClassType.Archer, 1, equipped);
        Assert.Equal(14 + 8 + 4, stats.Attack);
        Assert.Equal(4 + 4 + 3, stats.Defense);
    }
}
