// 서버 설정(appsettings Game 섹션)으로 바뀌는 운영 수치 묶음
using System.Collections.Generic;

namespace Ashveil.Shared
{
    // 목록 기본값은 비워 둔다. 설정 바인더가 기존 목록에 항목을 덧붙이기 때문에, 값은 appsettings.json 한 곳에만 둔다.
    public sealed class GameConfig
    {
        public EnhanceConfig Enhance { get; set; } = new EnhanceConfig();
        public AlignmentConfig Alignment { get; set; } = new AlignmentConfig();
        public DropConfig Drop { get; set; } = new DropConfig();
        public ShopConfig Shop { get; set; } = new ShopConfig();
        public BotConfig Bots { get; set; } = new BotConfig();
        public CombatConfig Combat { get; set; } = new CombatConfig();
    }

    public sealed class EnhanceRate
    {
        public int TargetLevel { get; set; }
        public double Success { get; set; }
        public double Fail { get; set; }
        public double Destroy { get; set; }
    }

    public sealed class EnhanceConfig
    {
        /// <summary>이 수치까지는 100% 성공한다.</summary>
        public int SafeLevel { get; set; } = 3;
        public int MaxLevel { get; set; } = 10;
        public long GoldCostPerLevel { get; set; } = 100;
        public List<EnhanceRate> Rates { get; set; } = new List<EnhanceRate>();
    }

    public sealed class AlignmentConfig
    {
        public int Min { get; set; } = -1000;
        public int Max { get; set; } = 1000;
        public int PurpleDurationSeconds { get; set; } = 60;
        public int KillInnocentPenalty { get; set; } = 300;
        public int MonsterKillRecovery { get; set; } = 10;
    }

    public sealed class DeathDropTier
    {
        /// <summary>성향치가 이 값 이하일 때 적용한다. 오름차순으로 처음 맞는 구간을 쓴다.</summary>
        public int MaxAlignment { get; set; }
        public double Chance { get; set; }
        public bool IncludeEquipped { get; set; }
    }

    public sealed class DropConfig
    {
        public double BotLootChance { get; set; } = 0.5;
        public double BotLootChanceChaotic { get; set; } = 0.8;
        public int GroundItemLifetimeSeconds { get; set; } = 180;
        public List<DeathDropTier> DeathDrop { get; set; } = new List<DeathDropTier>();
    }

    public sealed class ShopConfig
    {
        /// <summary>악명 캐릭터의 구매 가격 배율(%).</summary>
        public int ChaoticPricePercent { get; set; } = 120;
    }

    public sealed class BotConfig
    {
        public int RespawnSeconds { get; set; } = 60;
        /// <summary>부활 시 이 수량보다 적으면 구역 전리품 표에서 보충한다.</summary>
        public int MinEquipmentItems { get; set; } = 2;
        public int MinOtherItems { get; set; } = 2;
        public int InventoryCapacity { get; set; } = 40;
    }

    public sealed class CombatConfig
    {
        /// <summary>처치 보고 좌표 허용 오차(리쉬 반경에 더한다).</summary>
        public float KillRangeTolerance { get; set; } = 15f;
        /// <summary>리젠 시간에서 빼 주는 쿨다운 여유(초).</summary>
        public int RespawnToleranceSeconds { get; set; } = 2;
        /// <summary>바닥 아이템 줍기 허용 거리.</summary>
        public float PickupRange { get; set; } = 8f;
        /// <summary>봇 처치·PK 보고 좌표가 대상 구역 밖으로 벗어나도 되는 거리.</summary>
        public float ZoneTolerance { get; set; } = 20f;
    }
}
