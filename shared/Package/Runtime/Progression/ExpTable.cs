// 레벨별 필요 경험치와 경험치 획득·사망 손실 계산
namespace Ashveil.Shared
{
    public struct ExpResult
    {
        public readonly int Level;
        public readonly long Exp;
        public readonly bool LeveledUp;

        public ExpResult(int level, long exp, bool leveledUp)
        {
            Level = level; Exp = exp; LeveledUp = leveledUp;
        }
    }

    public static class ExpTable
    {
        public const int MaxLevel = 50;
        public const int DeathLossPercent = 10;

        public static long ExpToNext(int level) => 100L * level * (level + 1) / 2;

        public static ExpResult ApplyExp(int level, long exp, long gained)
        {
            if (level >= MaxLevel) return new ExpResult(MaxLevel, 0, false);

            var startLevel = level;
            exp += gained;
            while (level < MaxLevel && exp >= ExpToNext(level))
            {
                exp -= ExpToNext(level);
                level++;
            }
            if (level >= MaxLevel) exp = 0;
            return new ExpResult(level, exp, level > startLevel);
        }

        /// <summary>사망 시 잃는 경험치. 레벨은 내려가지 않는다.</summary>
        public static long DeathExpLoss(int level, long exp)
        {
            var loss = ExpToNext(level) * DeathLossPercent / 100;
            return loss < exp ? loss : exp;
        }
    }
}
