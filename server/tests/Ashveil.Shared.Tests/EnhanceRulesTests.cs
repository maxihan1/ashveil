// 강화 확률·비용·주문서 규칙 테스트
using Ashveil.Shared;
using Xunit;

namespace Ashveil.Shared.Tests;

public class EnhanceRulesTests
{
    private static EnhanceConfig Config() => new()
    {
        SafeLevel = 3,
        MaxLevel = 10,
        GoldCostPerLevel = 100,
        Rates = new List<EnhanceRate>
        {
            new() { TargetLevel = 4, Success = 0.7, Fail = 0.2, Destroy = 0.1 },
            new() { TargetLevel = 5, Success = 0.6, Fail = 0.25, Destroy = 0.15 },
        },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void SafeZone_AlwaysSucceeds(int currentLevel)
    {
        var rate = EnhanceRules.GetRate(Config(), currentLevel + 1);
        Assert.Equal(1.0, rate.Success);
        Assert.Equal(EnhanceResult.Success, EnhanceRules.Resolve(rate, 0.9999));
    }

    [Theory]
    [InlineData(0.0, EnhanceResult.Success)]
    [InlineData(0.6999, EnhanceResult.Success)]
    [InlineData(0.7, EnhanceResult.Fail)]
    [InlineData(0.8999, EnhanceResult.Fail)]
    [InlineData(0.9, EnhanceResult.Destroy)]
    [InlineData(0.9999, EnhanceResult.Destroy)]
    public void Resolve_UsesCumulativeBuckets(double roll, EnhanceResult expected)
    {
        var rate = EnhanceRules.GetRate(Config(), 4);
        Assert.Equal(expected, EnhanceRules.Resolve(rate, roll));
    }

    [Fact]
    public void MissingRow_UsesHighestDefinedRow()
    {
        var rate = EnhanceRules.GetRate(Config(), 9);
        Assert.Equal(0.6, rate.Success);
    }

    [Fact]
    public void GoldCost_ScalesWithCurrentLevel()
    {
        Assert.Equal(100, EnhanceRules.GoldCost(Config(), 0));
        Assert.Equal(400, EnhanceRules.GoldCost(Config(), 3));
    }

    [Fact]
    public void Validate_RejectsWrongScrollAndNonEquipment()
    {
        var cfg = Config();
        Assert.Null(EnhanceRules.Validate(cfg, ItemCatalog.Get("sword_short"), 0, ItemCatalog.Get("scroll_weapon")));
        Assert.Null(EnhanceRules.Validate(cfg, ItemCatalog.Get("helm_iron"), 0, ItemCatalog.Get("scroll_armor")));
        Assert.Equal(ErrorCodes.ScrollMismatch, EnhanceRules.Validate(cfg, ItemCatalog.Get("sword_short"), 0, ItemCatalog.Get("scroll_armor")));
        Assert.Equal(ErrorCodes.ScrollMismatch, EnhanceRules.Validate(cfg, ItemCatalog.Get("boots_leather"), 0, ItemCatalog.Get("scroll_weapon")));
        Assert.Equal(ErrorCodes.ScrollMismatch, EnhanceRules.Validate(cfg, ItemCatalog.Get("sword_short"), 0, ItemCatalog.Get("potion_hp_s")));
        Assert.Equal(ErrorCodes.NotEquippable, EnhanceRules.Validate(cfg, ItemCatalog.Get("potion_hp_s"), 0, ItemCatalog.Get("scroll_weapon")));
        Assert.Equal(ErrorCodes.MaxEnhance, EnhanceRules.Validate(cfg, ItemCatalog.Get("sword_short"), 10, ItemCatalog.Get("scroll_weapon")));
    }

    [Fact]
    public void Roll_UsesRandomSource()
    {
        var result = EnhanceRules.Roll(Config(), 3, new FixedRandom(0.95));
        Assert.Equal(EnhanceResult.Destroy, result);
    }
}

/// <summary>고정 값을 차례로 돌려주는 테스트용 난수.</summary>
internal sealed class FixedRandom : IRandomSource
{
    private readonly double[] _values;
    private int _index;

    public FixedRandom(params double[] values) { _values = values; }

    public double NextDouble() => _values[_index++ % _values.Length];

    public int Next(int minInclusive, int maxExclusive)
    {
        var v = NextDouble();
        return minInclusive + (int)Math.Floor(v * (maxExclusive - minInclusive));
    }
}
