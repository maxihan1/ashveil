// appsettings 기본 운영 수치 + GM 덮어쓰기(메모리)를 합친 현재 설정 공급자
using Ashveil.Shared;

namespace Ashveil.Server.Infrastructure;

/// <summary>
/// 읽는 쪽은 요청마다 속성을 한 번 읽어 쓴다. GM 변경은 새 객체로 통째로 바꾸므로 읽는 중에 내용이 바뀌지 않는다.
/// ponytail: 프로세스 메모리에만 둔다. 서버를 여러 대 띄우거나 재시작 후에도 유지해야 하면 DB 테이블로 옮긴다.
/// </summary>
public sealed class RuntimeGameConfig
{
    private readonly GameConfig _base;
    private EnhanceConfig _enhance;
    private DropConfig _drop;

    public RuntimeGameConfig(GameConfig baseConfig)
    {
        _base = baseConfig;
        _enhance = Clone(baseConfig.Enhance);
        _drop = Clone(baseConfig.Drop);
    }

    public EnhanceConfig Enhance => Volatile.Read(ref _enhance);
    public DropConfig Drop => Volatile.Read(ref _drop);
    public AlignmentConfig Alignment => _base.Alignment;
    public ShopConfig Shop => _base.Shop;
    public BotConfig Bots => _base.Bots;
    public CombatConfig Combat => _base.Combat;

    public void SetEnhanceRates(List<EnhanceRate>? rates)
    {
        var next = Clone(_base.Enhance);
        if (rates != null) next.Rates = rates.Select(Clone).ToList();
        Volatile.Write(ref _enhance, next);
    }

    public void SetDrop(double? botLoot, double? botLootChaotic, List<DeathDropTier>? deathDrop, bool reset)
    {
        var next = reset ? Clone(_base.Drop) : Clone(Drop);
        if (botLoot.HasValue) next.BotLootChance = botLoot.Value;
        if (botLootChaotic.HasValue) next.BotLootChanceChaotic = botLootChaotic.Value;
        if (deathDrop != null) next.DeathDrop = deathDrop.Select(Clone).ToList();
        Volatile.Write(ref _drop, next);
    }

    private static EnhanceConfig Clone(EnhanceConfig c) => new()
    {
        SafeLevel = c.SafeLevel, MaxLevel = c.MaxLevel, GoldCostPerLevel = c.GoldCostPerLevel,
        Rates = c.Rates.Select(Clone).ToList(),
    };

    private static EnhanceRate Clone(EnhanceRate r) => new()
    {
        TargetLevel = r.TargetLevel, Success = r.Success, Fail = r.Fail, Destroy = r.Destroy,
    };

    private static DropConfig Clone(DropConfig c) => new()
    {
        BotLootChance = c.BotLootChance, BotLootChanceChaotic = c.BotLootChanceChaotic,
        GroundItemLifetimeSeconds = c.GroundItemLifetimeSeconds, DeathDrop = c.DeathDrop.Select(Clone).ToList(),
    };

    private static DeathDropTier Clone(DeathDropTier t) => new()
    {
        MaxAlignment = t.MaxAlignment, Chance = t.Chance, IncludeEquipped = t.IncludeEquipped,
    };
}

/// <summary>서버 기본 난수. Random.Shared 는 스레드 안전하다.</summary>
public sealed class SharedRandomSource : IRandomSource
{
    public double NextDouble() => Random.Shared.NextDouble();
    public int Next(int minInclusive, int maxExclusive) => Random.Shared.Next(minInclusive, maxExclusive);
}
