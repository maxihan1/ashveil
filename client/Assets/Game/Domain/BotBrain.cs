// 봇 의사결정: 성향(평화·반격·선공)과 플레이어 성향에 따른 행동 선택
using Ashveil.Shared;

namespace Ashveil.Domain
{
    public enum BotActionKind { Wander, HuntMonster, AttackPlayer, Flee, PickupItem, GoToTown, Rest }

    /// <summary>봇이 판단에 쓰는 주변 정보.</summary>
    public struct BotSenses
    {
        public Vec2 Position;
        public float HpRatio;
        public bool InSafeZone;

        public bool PlayerVisible;
        public Vec2 PlayerPosition;
        public AlignmentState PlayerState;
        public bool PlayerInSafeZone;
        public bool PlayerDead;
        /// <summary>최근 일정 시간 안에 플레이어에게 맞았는지.</summary>
        public bool AttackedByPlayerRecently;

        public bool MonsterVisible;
        public Vec2 MonsterPosition;

        public bool GroundItemVisible;
        public Vec2 GroundItemPosition;

        /// <summary>마을 방문 차례인지(시간 기반으로 바깥에서 정한다).</summary>
        public bool TownVisitDue;
    }

    public static class BotBrain
    {
        public const float PlayerSightRange = 12f;
        public const float MonsterSightRange = 20f;
        public const float ItemSightRange = 15f;
        public const float LowHpRatio = 0.3f;

        public static BotActionKind Decide(Temperament temperament, AlignmentState selfState, BotSenses s)
        {
            var playerNear = s.PlayerVisible && !s.PlayerDead && Vec2.Distance(s.Position, s.PlayerPosition) <= PlayerSightRange;
            // 안전지대에서는 누구도 PK 할 수 없다.
            var pkPossible = playerNear && !s.PlayerInSafeZone && !s.InSafeZone;

            if (pkPossible && s.AttackedByPlayerRecently)
                return temperament == Temperament.Peaceful || s.HpRatio < LowHpRatio ? BotActionKind.Flee : BotActionKind.AttackPlayer;

            if (pkPossible && s.PlayerState == AlignmentState.Chaotic)
                return temperament == Temperament.Peaceful ? BotActionKind.Flee : BotActionKind.AttackPlayer;

            if (pkPossible && temperament == Temperament.Aggressive && s.HpRatio >= LowHpRatio)
                return BotActionKind.AttackPlayer;

            if (s.HpRatio < LowHpRatio) return BotActionKind.Rest;

            if (s.GroundItemVisible && Vec2.Distance(s.Position, s.GroundItemPosition) <= ItemSightRange)
                return BotActionKind.PickupItem;

            // 악명 봇은 경비병이 있는 마을에 가지 않는다.
            if (s.TownVisitDue && selfState != AlignmentState.Chaotic) return BotActionKind.GoToTown;

            if (s.MonsterVisible && Vec2.Distance(s.Position, s.MonsterPosition) <= MonsterSightRange)
                return BotActionKind.HuntMonster;

            return BotActionKind.Wander;
        }

        /// <summary>봇 ID로 구역 안의 결정적 출현 지점을 고른다(마을·다른 구역 좌표는 건너뛴다).</summary>
        public static Vec2 SpawnPoint(long botId, ZoneId zone, int attempt = 0)
        {
            var bounds = ZoneMap.BoundsOf(zone);
            for (var i = 0; i < 64; i++)
            {
                var h = Hash(botId * 131 + attempt * 977 + i);
                var x = bounds.MinX + 10f + (bounds.MaxX - bounds.MinX - 20f) * ((h & 0xFFFF) / 65535f);
                var z = bounds.MinZ + 10f + (bounds.MaxZ - bounds.MinZ - 20f) * (((h >> 16) & 0xFFFF) / 65535f);
                if (ZoneMap.GetZone(x, z) == zone) return new Vec2(x, z);
            }
            var center = ZoneMap.CenterOf(zone);
            return new Vec2(center.X, center.Z);
        }

        private static uint Hash(long value)
        {
            unchecked
            {
                var x = (uint)value ^ (uint)(value >> 32);
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
