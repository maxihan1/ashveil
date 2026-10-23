// 사망 드랍·봇 전리품 확률 판정과 드랍 후보 선택 규칙
using System.Collections.Generic;

namespace Ashveil.Shared
{
    public struct DropRoll
    {
        public readonly bool Drops;
        public readonly bool IncludeEquipped;

        public DropRoll(bool drops, bool includeEquipped)
        {
            Drops = drops; IncludeEquipped = includeEquipped;
        }
    }

    public struct DropCandidate
    {
        public readonly long ItemId;
        public readonly bool Equipped;

        public DropCandidate(long itemId, bool equipped)
        {
            ItemId = itemId; Equipped = equipped;
        }
    }

    public static class DropRules
    {
        public static DeathDropTier GetDeathDropTier(DropConfig config, int alignment)
        {
            DeathDropTier match = null;
            DeathDropTier highest = null;
            foreach (var tier in config.DeathDrop)
            {
                if (highest == null || tier.MaxAlignment > highest.MaxAlignment) highest = tier;
                if (alignment <= tier.MaxAlignment && (match == null || tier.MaxAlignment < match.MaxAlignment)) match = tier;
            }
            return match ?? highest ?? new DeathDropTier { MaxAlignment = int.MaxValue, Chance = 0 };
        }

        public static DropRoll RollDeathDrop(DropConfig config, int alignment, IRandomSource random)
        {
            var tier = GetDeathDropTier(config, alignment);
            return new DropRoll(random.NextDouble() < tier.Chance, tier.IncludeEquipped);
        }

        /// <summary>봇 처치 전리품. 악명 봇은 확률이 높고 장착 장비도 후보에 들어간다.</summary>
        public static DropRoll RollBotLoot(DropConfig config, AlignmentState botState, IRandomSource random)
        {
            var chaotic = botState == AlignmentState.Chaotic;
            var chance = chaotic ? config.BotLootChanceChaotic : config.BotLootChance;
            return new DropRoll(random.NextDouble() < chance, chaotic);
        }

        /// <summary>후보 중 하나를 고른다. 없으면 null.</summary>
        public static long? PickCandidate(IReadOnlyList<DropCandidate> items, bool includeEquipped, IRandomSource random)
        {
            var pool = new List<long>();
            foreach (var item in items)
                if (includeEquipped || !item.Equipped) pool.Add(item.ItemId);
            if (pool.Count == 0) return null;
            return pool[random.Next(0, pool.Count)];
        }
    }
}
