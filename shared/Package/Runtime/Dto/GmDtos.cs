// QA 환경 전용 GM API 요청·응답 DTO
using System.Collections.Generic;

namespace Ashveil.Shared.Dto
{
    public sealed class GmGoldRequest
    {
        public long Amount { get; set; }
    }

    public sealed class GmItemRequest
    {
        public string TemplateId { get; set; }
        public int Quantity { get; set; } = 1;
        public int EnhanceLevel { get; set; }
    }

    public sealed class GmLevelRequest
    {
        public int Level { get; set; }
    }

    public sealed class GmEnhanceRateRequest
    {
        public List<EnhanceRate> Rates { get; set; }
        public bool Reset { get; set; }
    }

    public sealed class GmBotResetRequest
    {
        public ZoneId? Zone { get; set; }
    }

    public sealed class GmBotResetResponse
    {
        public int BotCount { get; set; }
    }

    public sealed class GmAlignmentRequest
    {
        public int Value { get; set; }
        public bool ClearPurple { get; set; } = true;
    }

    public sealed class GmDropRateRequest
    {
        public double? BotLootChance { get; set; }
        public double? BotLootChanceChaotic { get; set; }
        public List<DeathDropTier> DeathDrop { get; set; }
        public bool Reset { get; set; }
    }

    public sealed class GmConfigResponse
    {
        public EnhanceConfig Enhance { get; set; }
        public DropConfig Drop { get; set; }
    }
}
