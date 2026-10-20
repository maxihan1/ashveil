// 상점·강화·전투·월드·봇·원장 API DTO
using System;
using System.Collections.Generic;

namespace Ashveil.Shared.Dto
{
    public sealed class ShopItemDto
    {
        public string TemplateId { get; set; }
        public long Price { get; set; }
    }

    public sealed class ShopItemsResponse
    {
        public double PriceMultiplier { get; set; }
        public List<ShopItemDto> Items { get; set; } = new List<ShopItemDto>();
    }

    public sealed class BuyRequest
    {
        public string TemplateId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class SellRequest
    {
        public long ItemId { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class EnhanceRequest
    {
        public long ItemId { get; set; }
        public long ScrollId { get; set; }
    }

    public sealed class EnhanceResponse
    {
        public EnhanceResult Result { get; set; }
        /// <summary>파괴되면 null.</summary>
        public ItemDto Item { get; set; }
        public int FromLevel { get; set; }
        public int ToLevel { get; set; }
        public long GoldSpent { get; set; }
        public InventoryDto Inventory { get; set; }
    }

    public sealed class MonsterKillRequest
    {
        public MonsterType MonsterId { get; set; }
        public string SpawnId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class MonsterKillResponse
    {
        public long GoldGained { get; set; }
        public long ExpGained { get; set; }
        public bool LeveledUp { get; set; }
        public CharacterDto Character { get; set; }
    }

    public sealed class BotKillRequest
    {
        public long BotId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class BotKillResponse
    {
        public ItemDto Dropped { get; set; }
        public bool DroppedToGround { get; set; }
        public long? GroundItemId { get; set; }
        public int AlignmentDelta { get; set; }
        public CharacterDto Character { get; set; }
        public BotDto Bot { get; set; }
    }

    public sealed class PkAttackRequest
    {
        public EntityKind AttackerKind { get; set; } = EntityKind.Character;
        public long AttackerId { get; set; }
        public EntityKind TargetKind { get; set; } = EntityKind.Bot;
        public long TargetId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class PkAttackResponse
    {
        public AlignmentDto Attacker { get; set; }
        public AlignmentDto Target { get; set; }
        public bool AttackerBecamePurple { get; set; }
    }

    public sealed class PlayerDeathRequest
    {
        public EntityKind KillerType { get; set; }
        public long? KillerId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class PlayerDeathResponse
    {
        public long ExpLost { get; set; }
        public GroundItemDto Dropped { get; set; }
        public CharacterDto Character { get; set; }
    }

    public sealed class GroundItemDto
    {
        public long Id { get; set; }
        public string TemplateId { get; set; }
        public int Quantity { get; set; }
        public int EnhanceLevel { get; set; }
        public ZoneId Zone { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public sealed class PickupRequest
    {
        /// <summary>값이 있으면 그 봇이 줍는다.</summary>
        public long? BotId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
    }

    public sealed class PickupResponse
    {
        public ItemDto Item { get; set; }
        public EntityKind PickedByKind { get; set; }
        public long PickedById { get; set; }
        /// <summary>캐릭터가 주웠을 때만 채운다.</summary>
        public InventoryDto Inventory { get; set; }
    }

    public sealed class BotDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public ClassType ClassType { get; set; }
        public Gender Gender { get; set; }
        public int Level { get; set; }
        public ZoneId Zone { get; set; }
        public Temperament Temperament { get; set; }
        public AlignmentDto Alignment { get; set; }
        public bool IsDead { get; set; }
        public DateTime? RespawnAt { get; set; }
        public List<ItemDto> Equipment { get; set; } = new List<ItemDto>();
        public int ItemCount { get; set; }
    }

    public sealed class LedgerEntryDto
    {
        public long Id { get; set; }
        public string Reason { get; set; }
        public long Delta { get; set; }
        public long BalanceAfter { get; set; }
        public string RelatedType { get; set; }
        public string RelatedId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class LedgerReasons
    {
        public const string CharacterCreated = "CHARACTER_CREATED";
        public const string MonsterKill = "MONSTER_KILL";
        public const string ShopBuy = "SHOP_BUY";
        public const string ShopSell = "SHOP_SELL";
        public const string Enhance = "ENHANCE";
        public const string GmGrant = "GM_GRANT";
    }
}
