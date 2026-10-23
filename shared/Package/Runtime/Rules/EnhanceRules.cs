// 강화 확률 조회, 결과 판정, 비용, 주문서 검증 규칙
namespace Ashveil.Shared
{
    public static class EnhanceRules
    {
        public static EnhanceRate GetRate(EnhanceConfig config, int targetLevel)
        {
            if (targetLevel <= config.SafeLevel)
                return new EnhanceRate { TargetLevel = targetLevel, Success = 1.0 };

            EnhanceRate best = null;
            foreach (var rate in config.Rates)
            {
                if (rate.TargetLevel == targetLevel) return rate;
                // 정의되지 않은 수치는 그보다 낮은 수치 중 가장 높은 행을 쓴다.
                if (rate.TargetLevel < targetLevel && (best == null || rate.TargetLevel > best.TargetLevel)) best = rate;
            }
            return best ?? new EnhanceRate { TargetLevel = targetLevel, Success = 1.0 };
        }

        /// <summary>roll ∈ [0,1) 을 성공 → 실패 → 파괴 순서의 누적 구간에 대응시킨다.</summary>
        public static EnhanceResult Resolve(EnhanceRate rate, double roll)
        {
            if (roll < rate.Success) return EnhanceResult.Success;
            if (roll < rate.Success + rate.Fail) return EnhanceResult.Fail;
            return EnhanceResult.Destroy;
        }

        public static EnhanceResult Roll(EnhanceConfig config, int currentLevel, IRandomSource random) =>
            Resolve(GetRate(config, currentLevel + 1), random.NextDouble());

        public static long GoldCost(EnhanceConfig config, int currentLevel) => config.GoldCostPerLevel * (currentLevel + 1);

        /// <summary>문제가 없으면 null, 있으면 에러 코드.</summary>
        public static string Validate(EnhanceConfig config, ItemTemplate target, int currentLevel, ItemTemplate scroll)
        {
            if (!target.IsEquipment) return ErrorCodes.NotEquippable;
            if (currentLevel >= config.MaxLevel) return ErrorCodes.MaxEnhance;
            var needed = target.Type == ItemType.Weapon ? ScrollKind.Weapon : ScrollKind.Armor;
            if (scroll.Type != ItemType.Scroll || scroll.ScrollFor != needed) return ErrorCodes.ScrollMismatch;
            return null;
        }
    }
}
