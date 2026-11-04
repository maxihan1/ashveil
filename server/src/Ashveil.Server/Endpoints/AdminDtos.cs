// 어드민 API 요청·응답 DTO (QA 테스트 도구와 어드민 웹이 쓴다)
using Ashveil.Shared;

namespace Ashveil.Server.Endpoints;

public sealed class AdminAccountDto
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int CharacterCount { get; set; }
}

public sealed class AdminCharacterDto
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public ClassType ClassType { get; set; }
    public int Level { get; set; }
    public long Exp { get; set; }
    public long Gold { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public ZoneId Zone { get; set; }
    public int Alignment { get; set; }
    public DateTime? PurpleUntil { get; set; }
    public int PkKills { get; set; }
    public int PkDeaths { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Deleted { get; set; }
}

/// <summary>값을 준 항목만 바꾼다. 골드는 차액만큼 원장(ADMIN_ADJUST)에 남는다.</summary>
public sealed class AdminCharacterUpdate
{
    public long? Gold { get; set; }
    public int? Level { get; set; }
    public long? Exp { get; set; }
    public int? Alignment { get; set; }
    public bool ClearPurple { get; set; }
    public float? X { get; set; }
    public float? Z { get; set; }
}

public sealed class AdminEnhanceHistoryDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string TemplateId { get; set; } = "";
    public int FromLevel { get; set; }
    public int ToLevel { get; set; }
    public EnhanceResult Result { get; set; }
    public long GoldCost { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>다음 판정 결과를 차례로 정해 둔다. 비어 있는 항목은 그대로 둔다.</summary>
public sealed class AdminForceRequest
{
    public List<EnhanceResult>? Enhance { get; set; }
    public List<bool>? BotLoot { get; set; }
    public List<bool>? DeathDrop { get; set; }
}

public sealed class AdminForcedDto
{
    public List<EnhanceResult> Enhance { get; set; } = [];
    public List<bool> BotLoot { get; set; } = [];
    public List<bool> DeathDrop { get; set; } = [];
}

public sealed class AdminConfigDto
{
    public EnhanceConfig Enhance { get; set; } = new();
    public DropConfig Drop { get; set; } = new();
    public AdminForcedDto Forced { get; set; } = new();
}

public sealed class AdminCleanupRequest
{
    /// <summary>이 문자열로 시작하는 계정을 지운다. 실수로 전체를 지우지 않도록 3자 이상이어야 한다.</summary>
    public string UsernamePrefix { get; set; } = "";
}

public sealed class AdminCleanupResponse
{
    public int AccountsDeleted { get; set; }
    public int CharactersDeleted { get; set; }
}

public sealed class AdminWorldResetRequest
{
    public ZoneId? Zone { get; set; }
}

public sealed class AdminWorldResetResponse
{
    public int GroundItemsDeleted { get; set; }
    public int BotCount { get; set; }
}

public sealed class AdminSpawnRequest
{
    public ZoneId Zone { get; set; }
    public MonsterType Monster { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

public sealed class AdminCountResponse
{
    public int Count { get; set; }
}
