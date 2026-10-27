// 엔티티를 shared DTO로 바꾸는 매핑 모음
using Ashveil.Server.Data;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Server.Services;

public static class Map
{
    public static AlignmentDto Alignment(int value, DateTime? purpleUntil, DateTime now) => new()
    {
        Value = value,
        State = AlignmentRules.GetState(value, purpleUntil, now),
        PurpleUntil = purpleUntil.HasValue && purpleUntil.Value > now ? purpleUntil : null,
    };

    public static StatsDto Stats(Stats s) => new() { MaxHp = s.MaxHp, MaxMp = s.MaxMp, Attack = s.Attack, Defense = s.Defense };

    public static CharacterDto Character(Character c, IEnumerable<Item> items, DateTime now) => new()
    {
        Id = c.Id,
        Name = c.Name,
        ClassType = c.ClassType,
        Gender = c.Gender,
        Level = c.Level,
        Exp = c.Exp,
        ExpToNext = ExpTable.ExpToNext(c.Level),
        Gold = c.Gold,
        Position = new Vector3Dto { X = c.PosX, Y = c.PosY, Z = c.PosZ },
        Zone = ZoneMap.GetZone(c.PosX, c.PosZ),
        Stats = Stats(GameOps.ComputeStats(c.ClassType, c.Level, items)),
        Alignment = Alignment(c.Alignment, c.PurpleUntil, now),
        PkKills = c.PkKills,
        PkDeaths = c.PkDeaths,
    };

    public static CharacterSummaryDto Summary(Character c) => new()
    {
        Id = c.Id, Name = c.Name, ClassType = c.ClassType, Gender = c.Gender, Level = c.Level,
        Zone = ZoneMap.GetZone(c.PosX, c.PosZ),
    };

    public static ItemDto Item(Item i) => new()
    {
        Id = i.Id,
        TemplateId = i.TemplateId,
        Quantity = i.Quantity,
        EnhanceLevel = i.EnhanceLevel,
        EquippedSlot = i.EquippedSlot,
        SellPrice = ItemCatalog.TryGet(i.TemplateId, out var t) ? ItemRules.SellPrice(t, i.EnhanceLevel) : 0,
    };

    public static InventoryDto Inventory(Character c, List<Item> items) => new()
    {
        Capacity = InventoryRules.Capacity,
        Used = items.Count,
        Gold = c.Gold,
        Items = items.Select(Item).ToList(),
    };

    public static BotDto Bot(Bot b, IEnumerable<Item> items, DateTime now)
    {
        var list = items.ToList();
        return new BotDto
        {
            Id = b.Id,
            Name = b.Name,
            ClassType = b.ClassType,
            Gender = b.Gender,
            Level = b.Level,
            Zone = b.Zone,
            Temperament = b.Temperament,
            Alignment = Alignment(b.Alignment, b.PurpleUntil, now),
            IsDead = b.IsDead,
            RespawnAt = b.IsDead ? b.RespawnAt : null,
            Equipment = list.Where(i => i.EquippedSlot != null).Select(Item).ToList(),
            ItemCount = list.Count,
        };
    }

    public static GroundItemDto Ground(GroundItem g) => new()
    {
        Id = g.ItemId,
        TemplateId = g.Item.TemplateId,
        Quantity = g.Item.Quantity,
        EnhanceLevel = g.Item.EnhanceLevel,
        Zone = g.Zone,
        X = g.X,
        Z = g.Z,
        ExpiresAt = g.ExpiresAt,
    };

    public static LedgerEntryDto Ledger(LedgerEntry e) => new()
    {
        Id = e.Id, Reason = e.Reason, Delta = e.Delta, BalanceAfter = e.BalanceAfter,
        RelatedType = e.RelatedType, RelatedId = e.RelatedId, CreatedAt = e.CreatedAt,
    };
}
