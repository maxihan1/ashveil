// EF Core 엔티티 (계정, 캐릭터, 봇, 아이템, 바닥 아이템, 원장, 이력)
using Ashveil.Shared;

namespace Ashveil.Server.Data;

public class Account
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class RefreshToken
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

public class Character
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public string Name { get; set; } = "";
    /// <summary>이름 중복 검사용 소문자 키.</summary>
    public string NameKey { get; set; } = "";
    public ClassType ClassType { get; set; }
    public Gender Gender { get; set; }
    public int Level { get; set; } = 1;
    public long Exp { get; set; }
    public long Gold { get; set; }
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public int Alignment { get; set; }
    public DateTime? PurpleUntil { get; set; }
    public int PkKills { get; set; }
    public int PkDeaths { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public class Bot
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public ClassType ClassType { get; set; }
    public Gender Gender { get; set; }
    public int Level { get; set; }
    public ZoneId Zone { get; set; }
    public Temperament Temperament { get; set; }
    public int Alignment { get; set; }
    public DateTime? PurpleUntil { get; set; }
    public bool IsDead { get; set; }
    public DateTime? RespawnAt { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
}

public class Item
{
    public long Id { get; set; }
    public string TemplateId { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public int EnhanceLevel { get; set; }
    public OwnerKind OwnerKind { get; set; }
    /// <summary>캐릭터 ID 또는 봇 ID. 바닥이면 0.</summary>
    public long OwnerId { get; set; }
    public EquipSlot? EquippedSlot { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GroundItem
{
    public long ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public ZoneId Zone { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public DateTime DroppedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public EntityKind DroppedByKind { get; set; }
    public long DroppedById { get; set; }
}

public class LedgerEntry
{
    public long Id { get; set; }
    public long CharacterId { get; set; }
    public string Reason { get; set; } = "";
    public long Delta { get; set; }
    public long BalanceAfter { get; set; }
    public string? RelatedType { get; set; }
    public string? RelatedId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EnhanceHistory
{
    public long Id { get; set; }
    public long CharacterId { get; set; }
    public long ItemId { get; set; }
    public string TemplateId { get; set; } = "";
    public int FromLevel { get; set; }
    public int ToLevel { get; set; }
    public EnhanceResult Result { get; set; }
    public long GoldCost { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SpawnCooldown
{
    public long CharacterId { get; set; }
    public string SpawnId { get; set; } = "";
    public DateTime LastKilledAt { get; set; }
}

public class PkRecord
{
    public long Id { get; set; }
    public EntityKind KillerKind { get; set; }
    public long KillerId { get; set; }
    public string KillerName { get; set; } = "";
    public EntityKind VictimKind { get; set; }
    public long VictimId { get; set; }
    public string VictimName { get; set; } = "";
    public bool VictimWasInnocent { get; set; }
    public int AlignmentDelta { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AlignmentHistory
{
    public long Id { get; set; }
    public EntityKind EntityKind { get; set; }
    public long EntityId { get; set; }
    public int Delta { get; set; }
    public int After { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
