// 캐릭터·인벤토리·장비·PK 기록 DTO
using System;
using System.Collections.Generic;

namespace Ashveil.Shared.Dto
{
    public sealed class CharacterSummaryDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public ClassType ClassType { get; set; }
        public Gender Gender { get; set; }
        public int Level { get; set; }
        public ZoneId Zone { get; set; }
    }

    public sealed class CreateCharacterRequest
    {
        public string Name { get; set; }
        public ClassType ClassType { get; set; }
        public Gender Gender { get; set; }
    }

    public sealed class Vector3Dto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class StatsDto
    {
        public int MaxHp { get; set; }
        public int MaxMp { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
    }

    public sealed class AlignmentDto
    {
        public int Value { get; set; }
        public AlignmentState State { get; set; }
        public DateTime? PurpleUntil { get; set; }
    }

    public sealed class CharacterDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public ClassType ClassType { get; set; }
        public Gender Gender { get; set; }
        public int Level { get; set; }
        public long Exp { get; set; }
        public long ExpToNext { get; set; }
        public long Gold { get; set; }
        public Vector3Dto Position { get; set; }
        public ZoneId Zone { get; set; }
        public StatsDto Stats { get; set; }
        public AlignmentDto Alignment { get; set; }
        public int PkKills { get; set; }
        public int PkDeaths { get; set; }
    }

    public sealed class PositionRequest
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }

    public sealed class ItemDto
    {
        public long Id { get; set; }
        public string TemplateId { get; set; }
        public int Quantity { get; set; }
        public int EnhanceLevel { get; set; }
        public EquipSlot? EquippedSlot { get; set; }
        public long SellPrice { get; set; }
    }

    public sealed class InventoryDto
    {
        public int Capacity { get; set; }
        public int Used { get; set; }
        public long Gold { get; set; }
        public List<ItemDto> Items { get; set; } = new List<ItemDto>();
    }

    public sealed class EquipRequest
    {
        public long ItemId { get; set; }
    }

    public sealed class EquipResponse
    {
        public InventoryDto Inventory { get; set; }
        public StatsDto Stats { get; set; }
    }

    public sealed class PkRecordDto
    {
        public EntityKind KillerKind { get; set; }
        public string KillerName { get; set; }
        public EntityKind VictimKind { get; set; }
        public string VictimName { get; set; }
        public bool VictimWasInnocent { get; set; }
        public int AlignmentDelta { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class AlignmentHistoryDto
    {
        public int Delta { get; set; }
        public int After { get; set; }
        public string Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class PkHistoryDto
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public AlignmentDto Alignment { get; set; }
        public List<PkRecordDto> Records { get; set; } = new List<PkRecordDto>();
        public List<AlignmentHistoryDto> AlignmentHistory { get; set; } = new List<AlignmentHistoryDto>();
    }
}
