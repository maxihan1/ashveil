// 봇 시드(구역별 인원·장비·소지품), 지연 부활과 아이템 보충
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Services;

public sealed class BotService(GameDbContext db, RuntimeGameConfig config, IRandomSource random, TimeProvider clock)
{
    private sealed record ZonePopulation(int Count, int MinLevel, int MaxLevel, int MaxEnhance, string[] Loot);

    private static readonly Dictionary<ZoneId, ZonePopulation> Populations = new()
    {
        [ZoneId.Beginner] = new(8, 3, 7, 2,
            ["sword_short", "bow_short", "staff_oak", "helm_leather", "armor_leather", "gloves_leather", "boots_leather"]),
        [ZoneId.Intermediate] = new(10, 9, 15, 4,
            ["sword_long", "bow_long", "staff_oak", "helm_iron", "armor_chain", "gloves_iron", "boots_iron", "gloves_leather"]),
        [ZoneId.Boss] = new(2, 18, 22, 6,
            ["axe_battle", "armor_plate", "sword_long", "bow_long", "helm_iron", "armor_chain", "boots_iron"]),
    };

    private static readonly string[] NameHeads = ["카", "세", "벨", "도", "미", "라", "하", "로", "에", "티", "주", "네", "비", "아"];
    private static readonly string[] NameTails = ["렌", "릴", "온", "칸", "린", "스", "르", "네", "트", "엘"];

    public static IEnumerable<ZoneId> BotZones => Populations.Keys;

    /// <summary>봇이 하나도 없으면 전 구역을 시드한다(서버 시작 시).</summary>
    public async Task SeedIfEmptyAsync()
    {
        if (await db.Bots.AnyAsync()) return;
        await db.InTransactionAsync(async () =>
        {
            foreach (var zone in BotZones) await SeedZoneAsync(zone);
        });
    }

    /// <summary>구역 봇을 지우고 다시 만든다(GM 봇 리셋). 호출자가 트랜잭션을 연다.</summary>
    public async Task<int> ResetAsync(ZoneId? zone)
    {
        var zones = zone.HasValue ? [zone.Value] : BotZones.ToArray();
        var bots = await db.Bots.Where(b => zones.Contains(b.Zone)).ToListAsync();
        var ids = bots.Select(b => b.Id).ToList();
        await db.Items.Where(i => i.OwnerKind == OwnerKind.Bot && ids.Contains(i.OwnerId)).ExecuteDeleteAsync();
        db.Bots.RemoveRange(bots);
        await db.SaveChangesAsync();

        var count = 0;
        foreach (var z in zones.Where(Populations.ContainsKey)) count += await SeedZoneAsync(z);
        return count;
    }

    private async Task<int> SeedZoneAsync(ZoneId zone)
    {
        var population = Populations[zone];
        var now = clock.GetUtcNow().UtcDateTime;
        var usedNames = (await db.Bots.Select(b => b.Name).ToListAsync()).ToHashSet();
        usedNames.UnionWith(db.Bots.Local.Select(b => b.Name));

        // 성향 순번은 전 구역을 이어서 센다. 구역마다 0부터 세면 8·10·2명이 5의 배수가 아니라 합이 50·35·15% 가 된다.
        var order = Populations.Where(p => p.Key < zone).Sum(p => p.Value.Count);
        var created = new List<Bot>();
        for (var i = 0; i < population.Count; i++)
        {
            var classType = (ClassType)random.Next(0, 3);
            var bot = new Bot
            {
                Name = NextName(usedNames),
                ClassType = classType,
                Gender = (Gender)random.Next(0, 2),
                Level = random.Next(population.MinLevel, population.MaxLevel + 1),
                Zone = zone,
                // 평화형 40%, 반격형 40%, 선공형 20%
                Temperament = ((order + i) % 5) switch { 0 or 1 => Temperament.Peaceful, 2 or 3 => Temperament.Retaliate, _ => Temperament.Aggressive },
            };
            db.Bots.Add(bot);
            created.Add(bot);
        }
        await db.SaveChangesAsync();

        foreach (var bot in created)
        {
            var items = new List<Item>();
            EquipDefaults(bot, items, now);
            Replenish(bot, items, now);
        }
        await db.SaveChangesAsync();
        return created.Count;
    }

    /// <summary>
    /// 부활 시각이 지난 봇을 살리고 잃은 아이템을 보충한다. SKIP LOCKED 로 동시 호출이 같은 봇을 두 번 처리하지 않는다.
    /// </summary>
    public async Task ReviveDueAsync()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.InTransactionAsync(async () =>
        {
            var due = await db.Bots
                .FromSql($"SELECT * FROM bots WHERE is_dead AND respawn_at <= {now} FOR UPDATE SKIP LOCKED")
                .ToListAsync();
            foreach (var bot in due) await ReviveAsync(bot, now);
        });
    }

    /// <summary>잠긴 봇 한 명을 부활시킨다.</summary>
    public async Task ReviveAsync(Bot bot, DateTime now)
    {
        bot.IsDead = false;
        bot.RespawnAt = null;
        bot.PurpleUntil = null;
        var items = await db.LoadItemsAsync(OwnerKind.Bot, bot.Id);
        EquipDefaults(bot, items, now);
        Replenish(bot, items, now);
    }

    /// <summary>비어 있는 장비 부위를 소지품으로 채우고, 무기·갑옷이 없으면 구역 기본 장비를 새로 준다.</summary>
    private void EquipDefaults(Bot bot, List<Item> items, DateTime now)
    {
        foreach (var slot in Enum.GetValues<EquipSlot>())
        {
            if (items.Any(i => i.EquippedSlot == slot)) continue;
            var spare = items.FirstOrDefault(i => i.EquippedSlot == null && ItemCatalog.Get(i.TemplateId).Slot == slot);
            if (spare != null) { spare.EquippedSlot = slot; continue; }

            var templateId = DefaultEquipment(bot, slot);
            if (templateId == null) continue;
            var item = NewItem(bot, templateId, 1, RandomEnhance(bot.Zone), now);
            item.EquippedSlot = slot;
            items.Add(item);
        }
    }

    private void Replenish(Bot bot, List<Item> items, DateTime now)
    {
        var loot = Populations[bot.Zone].Loot;
        while (items.Count(i => i.EquippedSlot == null && ItemCatalog.Get(i.TemplateId).IsEquipment) < config.Bots.MinEquipmentItems)
            items.Add(NewItem(bot, loot[random.Next(0, loot.Length)], 1, RandomEnhance(bot.Zone), now));

        while (items.Count(i => !ItemCatalog.Get(i.TemplateId).IsEquipment) < config.Bots.MinOtherItems)
        {
            var templateId = items.Any(i => i.TemplateId == ItemCatalog.PotionHpSmall)
                ? (random.NextDouble() < 0.5 ? ItemCatalog.ScrollWeapon : ItemCatalog.ScrollArmor)
                : ItemCatalog.PotionHpSmall;
            if (items.Any(i => i.TemplateId == templateId)) templateId = ItemCatalog.PotionMpSmall;
            var quantity = templateId == ItemCatalog.PotionHpSmall || templateId == ItemCatalog.PotionMpSmall ? random.Next(5, 21) : 1;
            items.Add(NewItem(bot, templateId, quantity, 0, now));
        }
    }

    private Item NewItem(Bot bot, string templateId, int quantity, int enhance, DateTime now)
    {
        var item = new Item
        {
            TemplateId = templateId,
            Quantity = quantity,
            EnhanceLevel = ItemCatalog.Get(templateId).IsEquipment ? enhance : 0,
            OwnerKind = OwnerKind.Bot,
            OwnerId = bot.Id,
            CreatedAt = now,
        };
        db.Items.Add(item);
        return item;
    }

    private int RandomEnhance(ZoneId zone) => random.Next(0, Populations[zone].MaxEnhance + 1);

    private static string? DefaultEquipment(Bot bot, EquipSlot slot)
    {
        var tier = bot.Zone switch { ZoneId.Beginner => 0, ZoneId.Intermediate => 1, _ => 2 };
        return slot switch
        {
            EquipSlot.Weapon => (bot.ClassType, tier) switch
            {
                (ClassType.Warrior, 2) => "axe_battle",
                (ClassType.Warrior, 1) => "sword_long",
                (ClassType.Archer, >= 1) => "bow_long",
                _ => ItemCatalog.StarterWeapon(bot.ClassType),
            },
            EquipSlot.Armor => tier switch { 0 => "armor_leather", 1 => "armor_chain", _ => "armor_plate" },
            EquipSlot.Helmet => tier == 0 ? null : "helm_iron",
            EquipSlot.Gloves => tier == 2 ? "gloves_iron" : null,
            EquipSlot.Boots => tier == 2 ? "boots_iron" : null,
            _ => null,
        };
    }

    private string NextName(HashSet<string> used)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var name = NameHeads[random.Next(0, NameHeads.Length)] + NameTails[random.Next(0, NameTails.Length)]
                       + NameTails[random.Next(0, NameTails.Length)];
            if (used.Add(name)) return name;
        }
        var fallback = $"봇{used.Count + 1}";
        used.Add(fallback);
        return fallback;
    }
}
