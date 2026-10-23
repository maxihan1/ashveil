// 아이템 템플릿 정적 카탈로그 (상점 목록, 장비 스탯, 소모품 효과)
using System.Collections.Generic;

namespace Ashveil.Shared
{
    public sealed class ItemTemplate
    {
        public string Id { get; }
        public string Name { get; }
        public ItemType Type { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int HealHp { get; }
        public int HealMp { get; }
        /// <summary>0이면 상점에서 팔지 않는다.</summary>
        public long BuyPrice { get; }
        public long SellPrice { get; }
        public ScrollKind ScrollFor { get; }

        public ItemTemplate(string id, string name, ItemType type, long buyPrice, long sellPrice,
            int attack = 0, int defense = 0, int healHp = 0, int healMp = 0, ScrollKind scrollFor = ScrollKind.None)
        {
            Id = id; Name = name; Type = type; BuyPrice = buyPrice; SellPrice = sellPrice;
            Attack = attack; Defense = defense; HealHp = healHp; HealMp = healMp; ScrollFor = scrollFor;
        }

        public bool Stackable => Type == ItemType.Consumable || Type == ItemType.Scroll;

        public int MaxStack => Stackable ? ItemCatalog.MaxStack : 1;

        public bool ShopListed => BuyPrice > 0;

        /// <summary>장비가 아니면 null.</summary>
        public EquipSlot? Slot
        {
            get
            {
                switch (Type)
                {
                    case ItemType.Weapon: return EquipSlot.Weapon;
                    case ItemType.Helmet: return EquipSlot.Helmet;
                    case ItemType.Armor: return EquipSlot.Armor;
                    case ItemType.Gloves: return EquipSlot.Gloves;
                    case ItemType.Boots: return EquipSlot.Boots;
                    default: return null;
                }
            }
        }

        public bool IsEquipment => Slot.HasValue;
    }

    public static class ItemCatalog
    {
        public const int MaxStack = 999;

        public const string PotionHpSmall = "potion_hp_s";
        public const string PotionHpLarge = "potion_hp_l";
        public const string PotionMpSmall = "potion_mp_s";
        public const string ScrollWeapon = "scroll_weapon";
        public const string ScrollArmor = "scroll_armor";

        private static readonly List<ItemTemplate> Ordered = new List<ItemTemplate>
        {
            new ItemTemplate(PotionHpSmall, "빨간 물약", ItemType.Consumable, 30, 10, healHp: 60),
            new ItemTemplate(PotionHpLarge, "진한 빨간 물약", ItemType.Consumable, 120, 40, healHp: 200),
            new ItemTemplate(PotionMpSmall, "파란 물약", ItemType.Consumable, 40, 12, healMp: 40),
            new ItemTemplate(ScrollWeapon, "무기 강화 주문서", ItemType.Scroll, 500, 150, scrollFor: ScrollKind.Weapon),
            new ItemTemplate(ScrollArmor, "방어구 강화 주문서", ItemType.Scroll, 400, 120, scrollFor: ScrollKind.Armor),
            new ItemTemplate("sword_short", "짧은 검", ItemType.Weapon, 300, 100, attack: 8),
            new ItemTemplate("sword_long", "롱 소드", ItemType.Weapon, 1200, 400, attack: 14),
            new ItemTemplate("bow_short", "숏 보우", ItemType.Weapon, 320, 105, attack: 9),
            new ItemTemplate("bow_long", "롱 보우", ItemType.Weapon, 1300, 430, attack: 15),
            new ItemTemplate("staff_oak", "참나무 지팡이", ItemType.Weapon, 320, 105, attack: 9),
            new ItemTemplate("axe_battle", "배틀 액스", ItemType.Weapon, 0, 700, attack: 20),
            new ItemTemplate("helm_leather", "가죽 투구", ItemType.Helmet, 150, 50, defense: 2),
            new ItemTemplate("helm_iron", "철 투구", ItemType.Helmet, 600, 200, defense: 4),
            new ItemTemplate("armor_leather", "가죽 갑옷", ItemType.Armor, 250, 80, defense: 4),
            new ItemTemplate("armor_chain", "사슬 갑옷", ItemType.Armor, 1000, 330, defense: 8),
            new ItemTemplate("armor_plate", "판금 갑옷", ItemType.Armor, 0, 600, defense: 12),
            new ItemTemplate("gloves_leather", "가죽 장갑", ItemType.Gloves, 100, 30, defense: 1),
            new ItemTemplate("gloves_iron", "철 장갑", ItemType.Gloves, 500, 160, defense: 3),
            new ItemTemplate("boots_leather", "가죽 장화", ItemType.Boots, 100, 30, defense: 1),
            new ItemTemplate("boots_iron", "철 장화", ItemType.Boots, 500, 160, defense: 3),
        };

        private static readonly Dictionary<string, ItemTemplate> ById = BuildIndex();

        private static Dictionary<string, ItemTemplate> BuildIndex()
        {
            var map = new Dictionary<string, ItemTemplate>();
            foreach (var t in Ordered) map[t.Id] = t;
            return map;
        }

        public static IReadOnlyList<ItemTemplate> All => Ordered;

        public static ItemTemplate Get(string id) => ById[id];

        public static bool TryGet(string id, out ItemTemplate template)
        {
            template = null;
            return id != null && ById.TryGetValue(id, out template);
        }

        public static IEnumerable<ItemTemplate> ShopListed()
        {
            foreach (var t in Ordered)
                if (t.ShopListed) yield return t;
        }

        public static string StarterWeapon(ClassType classType)
        {
            switch (classType)
            {
                case ClassType.Archer: return "bow_short";
                case ClassType.Mage: return "staff_oak";
                default: return "sword_short";
            }
        }

        public const string StarterArmor = "armor_leather";
        public const int StarterPotions = 10;
        public const long StarterGold = 500;
    }
}
