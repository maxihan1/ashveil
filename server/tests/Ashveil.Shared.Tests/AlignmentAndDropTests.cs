// 성향치 상태 판정, PK 패널티, 사망 드랍·봇 전리품 규칙 테스트
using Ashveil.Shared;
using Xunit;

namespace Ashveil.Shared.Tests;

public class AlignmentAndDropTests
{
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

    private static AlignmentConfig Align() => new()
    {
        Min = -1000, Max = 1000, PurpleDurationSeconds = 60, KillInnocentPenalty = 300, MonsterKillRecovery = 10,
    };

    private static DropConfig Drop() => new()
    {
        BotLootChance = 0.5,
        BotLootChanceChaotic = 0.8,
        GroundItemLifetimeSeconds = 180,
        DeathDrop = new List<DeathDropTier>
        {
            new() { MaxAlignment = -600, Chance = 0.8, IncludeEquipped = true },
            new() { MaxAlignment = -300, Chance = 0.5, IncludeEquipped = true },
            new() { MaxAlignment = -1, Chance = 0.25, IncludeEquipped = false },
            new() { MaxAlignment = 1000, Chance = 0.05, IncludeEquipped = false },
        },
    };

    [Fact]
    public void State_ChaoticBeatsPurple_PurpleExpires()
    {
        Assert.Equal(AlignmentState.Innocent, AlignmentRules.GetState(0, null, Now));
        Assert.Equal(AlignmentState.Purple, AlignmentRules.GetState(0, Now.AddSeconds(5), Now));
        Assert.Equal(AlignmentState.Innocent, AlignmentRules.GetState(0, Now.AddSeconds(-1), Now));
        Assert.Equal(AlignmentState.Chaotic, AlignmentRules.GetState(-1, Now.AddSeconds(5), Now));
    }

    [Fact]
    public void AttackingInnocent_MakesAttackerPurple()
    {
        var until = AlignmentRules.PurpleUntilAfterAttack(Align(), AlignmentState.Innocent, Now);
        Assert.Equal(Now.AddSeconds(60), until);
        Assert.Null(AlignmentRules.PurpleUntilAfterAttack(Align(), AlignmentState.Purple, Now));
        Assert.Null(AlignmentRules.PurpleUntilAfterAttack(Align(), AlignmentState.Chaotic, Now));
    }

    [Fact]
    public void KillingInnocent_DropsAlignment_KillingPurpleOrChaoticIsFree()
    {
        var innocent = AlignmentRules.ApplyKill(Align(), 0, AlignmentState.Innocent);
        Assert.Equal(-300, innocent.NewAlignment);
        Assert.Equal(-300, innocent.Delta);
        Assert.True(innocent.VictimWasInnocent);

        var purple = AlignmentRules.ApplyKill(Align(), 0, AlignmentState.Purple);
        Assert.Equal(0, purple.NewAlignment);
        Assert.Equal(0, purple.Delta);
        Assert.False(purple.VictimWasInnocent);

        var chaotic = AlignmentRules.ApplyKill(Align(), 50, AlignmentState.Chaotic);
        Assert.Equal(50, chaotic.NewAlignment);
    }

    [Fact]
    public void Alignment_IsClampedToRange()
    {
        Assert.Equal(-1000, AlignmentRules.ApplyKill(Align(), -900, AlignmentState.Innocent).NewAlignment);
        Assert.Equal(1000, AlignmentRules.ApplyMonsterKill(Align(), 995));
        Assert.Equal(-290, AlignmentRules.ApplyMonsterKill(Align(), -300));
    }

    [Theory]
    [InlineData(500, 0.05, false)]
    [InlineData(0, 0.05, false)]
    [InlineData(-1, 0.25, false)]
    [InlineData(-299, 0.25, false)]
    [InlineData(-300, 0.5, true)]
    [InlineData(-600, 0.8, true)]
    [InlineData(-1000, 0.8, true)]
    public void DeathDropTier_SelectedByAlignment(int alignment, double chance, bool includeEquipped)
    {
        var tier = DropRules.GetDeathDropTier(Drop(), alignment);
        Assert.Equal(chance, tier.Chance);
        Assert.Equal(includeEquipped, tier.IncludeEquipped);
    }

    [Fact]
    public void PickCandidate_ExcludesEquippedUnlessAllowed()
    {
        var items = new List<DropCandidate> { new(1, true), new(2, false), new(3, true) };
        Assert.Equal(2, DropRules.PickCandidate(items, false, new FixedRandom(0.99)));
        Assert.Equal(3, DropRules.PickCandidate(items, true, new FixedRandom(0.99)));
        Assert.Null(DropRules.PickCandidate(new List<DropCandidate> { new(1, true) }, false, new FixedRandom(0.1)));
    }

    [Fact]
    public void RollDeathDrop_RespectsChance()
    {
        Assert.True(DropRules.RollDeathDrop(Drop(), -700, new FixedRandom(0.79)).Drops);
        Assert.False(DropRules.RollDeathDrop(Drop(), -700, new FixedRandom(0.8)).Drops);
        Assert.False(DropRules.RollDeathDrop(Drop(), 0, new FixedRandom(0.06)).Drops);
    }

    [Fact]
    public void RollBotLoot_ChaoticBotDropsMoreAndIncludesEquipment()
    {
        var innocent = DropRules.RollBotLoot(Drop(), AlignmentState.Innocent, new FixedRandom(0.6));
        Assert.False(innocent.Drops);

        var chaotic = DropRules.RollBotLoot(Drop(), AlignmentState.Chaotic, new FixedRandom(0.6));
        Assert.True(chaotic.Drops);
        Assert.True(chaotic.IncludeEquipped);

        var purple = DropRules.RollBotLoot(Drop(), AlignmentState.Purple, new FixedRandom(0.4));
        Assert.True(purple.Drops);
        Assert.False(purple.IncludeEquipped);
    }
}
