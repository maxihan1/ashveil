// 직업·레벨·장착 장비로 최종 스탯을 계산하는 규칙
using System.Collections.Generic;

namespace Ashveil.Shared
{
    public struct Stats
    {
        public readonly int MaxHp;
        public readonly int MaxMp;
        public readonly int Attack;
        public readonly int Defense;

        public Stats(int maxHp, int maxMp, int attack, int defense)
        {
            MaxHp = maxHp; MaxMp = maxMp; Attack = attack; Defense = defense;
        }

        public override string ToString() => $"HP {MaxHp} MP {MaxMp} ATK {Attack} DEF {Defense}";
    }

    public struct EquippedItem
    {
        public readonly string TemplateId;
        public readonly int EnhanceLevel;

        public EquippedItem(string templateId, int enhanceLevel)
        {
            TemplateId = templateId; EnhanceLevel = enhanceLevel;
        }
    }

    public static class StatRules
    {
        // 기본값, 레벨당 증가량 순서: HP, MP, 공격력, 방어력
        private static readonly Dictionary<ClassType, int[]> Table = new Dictionary<ClassType, int[]>
        {
            [ClassType.Warrior] = new[] { 120, 12, 30, 2, 12, 2, 6, 1 },
            [ClassType.Archer] = new[] { 90, 9, 40, 3, 14, 3, 4, 1 },
            [ClassType.Mage] = new[] { 80, 8, 70, 6, 15, 3, 3, 1 },
        };

        public static Stats Compute(ClassType classType, int level, IEnumerable<EquippedItem> equipped)
        {
            var t = Table[classType];
            var growth = level - 1;
            var hp = t[0] + t[1] * growth;
            var mp = t[2] + t[3] * growth;
            var atk = t[4] + t[5] * growth;
            var def = t[6] + t[7] * growth;

            foreach (var item in equipped)
            {
                if (!ItemCatalog.TryGet(item.TemplateId, out var template) || !template.IsEquipment) continue;
                atk += ItemRules.AttackOf(template, item.EnhanceLevel);
                def += ItemRules.DefenseOf(template, item.EnhanceLevel);
            }
            return new Stats(hp, mp, atk, def);
        }
    }
}
