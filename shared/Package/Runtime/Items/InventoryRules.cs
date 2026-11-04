// 인벤토리 칸 수·겹치기 규칙과 아이템 적재 계획 계산
using System.Collections.Generic;

namespace Ashveil.Shared
{
    /// <summary>인벤토리 한 칸(스택)의 읽기 전용 모습.</summary>
    public struct StackView
    {
        public readonly long Id;
        public readonly string TemplateId;
        public readonly int Quantity;

        public StackView(long id, string templateId, int quantity)
        {
            Id = id; TemplateId = templateId; Quantity = quantity;
        }
    }

    public sealed class AddPlan
    {
        public bool Fits { get; }
        /// <summary>기존 스택에 더할 양.</summary>
        public List<(long ItemId, int Add)> Merges { get; }
        /// <summary>새로 만들 스택들의 수량.</summary>
        public List<int> NewStacks { get; }

        public AddPlan(bool fits, List<(long, int)> merges, List<int> newStacks)
        {
            Fits = fits; Merges = merges; NewStacks = newStacks;
        }
    }

    public static class InventoryRules
    {
        public const int Capacity = 40;

        public static AddPlan PlanAdd(IReadOnlyList<StackView> current, string templateId, int quantity)
        {
            var template = ItemCatalog.Get(templateId);
            var merges = new List<(long, int)>();
            var newStacks = new List<int>();
            var remaining = quantity;

            if (template.Stackable)
            {
                foreach (var stack in current)
                {
                    if (remaining <= 0) break;
                    if (stack.TemplateId != templateId || stack.Quantity >= template.MaxStack) continue;
                    var add = System.Math.Min(remaining, template.MaxStack - stack.Quantity);
                    merges.Add((stack.Id, add));
                    remaining -= add;
                }
            }

            while (remaining > 0)
            {
                var n = System.Math.Min(remaining, template.MaxStack);
                newStacks.Add(n);
                remaining -= n;
            }

            var free = Capacity - current.Count;
            return new AddPlan(newStacks.Count <= free, merges, newStacks);
        }
    }

    public static class ItemRules
    {
        public const int WeaponAttackPerEnhance = 2;
        public const int ArmorDefensePerEnhance = 1;

        /// <summary>강화된 장비의 판매가 = 기본 판매가 × (1 + 강화/2), 버림.</summary>
        public static long SellPrice(ItemTemplate template, int enhanceLevel) =>
            template.SellPrice * (2 + enhanceLevel) / 2;

        public static int AttackOf(ItemTemplate template, int enhanceLevel) =>
            template.Attack + (template.Type == ItemType.Weapon ? WeaponAttackPerEnhance * enhanceLevel : 0);

        public static int DefenseOf(ItemTemplate template, int enhanceLevel) =>
            template.Defense + (template.IsEquipment && template.Type != ItemType.Weapon ? ArmorDefensePerEnhance * enhanceLevel : 0);
    }
}
