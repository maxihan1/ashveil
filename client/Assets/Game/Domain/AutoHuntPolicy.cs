// 자동사냥 정책: 물약 자동 사용, 대상 유지·재탐색 판단
namespace Ashveil.Domain
{
    public enum AutoHuntAction { None, UsePotion, KeepTarget, FindTarget }

    public struct AutoHuntInput
    {
        public float HpRatio;
        public int PotionCount;
        public bool PotionReady;
        public bool HasLiveTarget;
    }

    public static class AutoHuntPolicy
    {
        /// <summary>HP가 이 비율 이하이면 물약을 마신다.</summary>
        public const float PotionThreshold = 0.5f;
        /// <summary>자동사냥이 대상을 찾는 반경(m).</summary>
        public const float SearchRadius = 25f;

        public static AutoHuntAction Decide(AutoHuntInput input)
        {
            if (input.HpRatio <= PotionThreshold && input.PotionCount > 0 && input.PotionReady) return AutoHuntAction.UsePotion;
            return input.HasLiveTarget ? AutoHuntAction.KeepTarget : AutoHuntAction.FindTarget;
        }
    }
}
