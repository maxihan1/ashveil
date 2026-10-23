// 아이템 이름·스탯 설명 문구와 원장 사유 한국어 라벨
using System.Text;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Presentation
{
    public static class ItemText
    {
        public static string NameOf(string templateId, int enhance = 0, int quantity = 1)
        {
            var name = ItemCatalog.TryGet(templateId, out var t) ? t.Name : templateId;
            if (enhance > 0) name = "+" + enhance + " " + name;
            if (quantity > 1) name += " x" + quantity;
            return name;
        }

        public static string TypeLabel(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return "소모품";
                case ItemType.Scroll: return "주문서";
                case ItemType.Weapon: return "무기";
                case ItemType.Helmet: return "투구";
                case ItemType.Armor: return "갑옷";
                case ItemType.Gloves: return "장갑";
                default: return "신발";
            }
        }

        public static string Describe(ItemDto item)
        {
            if (!ItemCatalog.TryGet(item.TemplateId, out var t)) return item.TemplateId;
            var sb = new StringBuilder();
            sb.Append("종류: ").Append(TypeLabel(t.Type)).Append('\n');
            if (t.IsEquipment)
            {
                var attack = ItemRules.AttackOf(t, item.EnhanceLevel);
                var defense = ItemRules.DefenseOf(t, item.EnhanceLevel);
                if (attack > 0) sb.Append("공격력 +").Append(attack).Append('\n');
                if (defense > 0) sb.Append("방어력 +").Append(defense).Append('\n');
                sb.Append("강화: +").Append(item.EnhanceLevel).Append('\n');
            }
            if (t.HealHp > 0) sb.Append("HP ").Append(t.HealHp).Append(" 회복\n");
            if (t.HealMp > 0) sb.Append("MP ").Append(t.HealMp).Append(" 회복\n");
            if (t.ScrollFor == ScrollKind.Weapon) sb.Append("무기를 +1 강화합니다.\n");
            if (t.ScrollFor == ScrollKind.Armor) sb.Append("방어구를 +1 강화합니다.\n");
            if (item.Quantity > 1) sb.Append("수량: ").Append(item.Quantity).Append('\n');
            sb.Append("판매가: ").Append(item.SellPrice.ToString("N0")).Append(" 골드");
            if (item.EquippedSlot != null) sb.Append("\n<color=#FFD24D>장착 중</color>");
            return sb.ToString();
        }

        public static string LedgerReason(string reason)
        {
            switch (reason)
            {
                case LedgerReasons.CharacterCreated: return "캐릭터 생성";
                case LedgerReasons.MonsterKill: return "몬스터 처치";
                case LedgerReasons.ShopBuy: return "상점 구매";
                case LedgerReasons.ShopSell: return "상점 판매";
                case LedgerReasons.Enhance: return "강화 비용";
                case LedgerReasons.GmGrant: return "GM 지급";
                default: return reason;
            }
        }
    }
}
