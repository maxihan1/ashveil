// 성향치(선량·보라·악명) 상태 판정과 PK·사냥에 따른 변화 규칙
using System;

namespace Ashveil.Shared
{
    public struct KillOutcome
    {
        public readonly int NewAlignment;
        public readonly int Delta;
        public readonly bool VictimWasInnocent;

        public KillOutcome(int newAlignment, int delta, bool victimWasInnocent)
        {
            NewAlignment = newAlignment; Delta = delta; VictimWasInnocent = victimWasInnocent;
        }
    }

    public static class AlignmentRules
    {
        /// <summary>악명(성향치 &lt; 0) → 보라(선공 타이머) → 선량 순서로 판정한다.</summary>
        public static AlignmentState GetState(int alignment, DateTime? purpleUntil, DateTime now)
        {
            if (alignment < 0) return AlignmentState.Chaotic;
            if (purpleUntil.HasValue && purpleUntil.Value > now) return AlignmentState.Purple;
            return AlignmentState.Innocent;
        }

        /// <summary>선량한 대상을 먼저 공격하면 새 보라 만료 시각, 아니면 null(변화 없음).</summary>
        public static DateTime? PurpleUntilAfterAttack(AlignmentConfig config, AlignmentState targetState, DateTime now) =>
            targetState == AlignmentState.Innocent ? now.AddSeconds(config.PurpleDurationSeconds) : (DateTime?)null;

        public static KillOutcome ApplyKill(AlignmentConfig config, int killerAlignment, AlignmentState victimState)
        {
            if (victimState != AlignmentState.Innocent) return new KillOutcome(killerAlignment, 0, false);
            var next = Clamp(config, killerAlignment - config.KillInnocentPenalty);
            return new KillOutcome(next, next - killerAlignment, true);
        }

        public static int ApplyMonsterKill(AlignmentConfig config, int alignment) =>
            Clamp(config, alignment + config.MonsterKillRecovery);

        public static int Clamp(AlignmentConfig config, int value) =>
            value < config.Min ? config.Min : (value > config.Max ? config.Max : value);
    }
}
