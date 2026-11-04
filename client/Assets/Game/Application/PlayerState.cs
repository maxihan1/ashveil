// 클라이언트가 보여 줄 내 캐릭터 상태(서버 응답 반영) + 런타임 HP·MP
using System;
using System.Collections.Generic;
using System.Linq;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Application
{
    /// <summary>
    /// 골드·인벤토리·스탯·성향은 서버 응답 값만 반영한다(클라이언트가 계산하지 않는다).
    /// 서버 응답이 오면 반드시 Apply 를 호출해 화면이 서버 상태와 어긋나지 않게 한다.
    /// </summary>
    public sealed class PlayerState
    {
        public CharacterDto Character { get; private set; }
        public InventoryDto Inventory { get; private set; }
        public int Hp { get; private set; }
        public int Mp { get; private set; }

        public event Action CharacterChanged;
        public event Action InventoryChanged;
        public event Action VitalsChanged;

        public long Gold => Character?.Gold ?? 0;
        public int MaxHp => Character?.Stats?.MaxHp ?? 1;
        public int MaxMp => Character?.Stats?.MaxMp ?? 1;
        public bool IsDead => Character != null && Hp <= 0;
        public AlignmentState AlignmentState => Character?.Alignment?.State ?? AlignmentState.Innocent;

        public void Reset()
        {
            Character = null;
            Inventory = null;
            Hp = Mp = 0;
        }

        public void Apply(CharacterDto character, bool fillVitals = false)
        {
            if (character == null) return;
            var first = Character == null;
            Character = character;
            if (Inventory != null) Inventory.Gold = character.Gold;
            // 사망 후 늦게 도착한 응답(레벨업 등)이 죽은 캐릭터를 되살리지 않게 한다. 부활은 RestoreVitals 로만.
            if (first || (fillVitals && !IsDead)) RestoreVitals();
            else ClampVitals();
            CharacterChanged?.Invoke();
        }

        public void Apply(InventoryDto inventory)
        {
            if (inventory == null) return;
            Inventory = inventory;
            if (Character != null && Character.Gold != inventory.Gold)
            {
                Character.Gold = inventory.Gold;
                CharacterChanged?.Invoke();
            }
            InventoryChanged?.Invoke();
        }

        public void Apply(StatsDto stats)
        {
            if (stats == null || Character == null) return;
            Character.Stats = stats;
            ClampVitals();
            CharacterChanged?.Invoke();
        }

        public void Apply(AlignmentDto alignment)
        {
            if (alignment == null || Character == null) return;
            Character.Alignment = alignment;
            CharacterChanged?.Invoke();
        }

        public void RestoreVitals()
        {
            Hp = MaxHp;
            Mp = MaxMp;
            VitalsChanged?.Invoke();
        }

        public void Damage(int amount)
        {
            Hp = Math.Max(0, Hp - Math.Max(0, amount));
            VitalsChanged?.Invoke();
        }

        public void Heal(int hp, int mp)
        {
            if (IsDead) return;
            Hp = Math.Min(MaxHp, Hp + Math.Max(0, hp));
            Mp = Math.Min(MaxMp, Mp + Math.Max(0, mp));
            VitalsChanged?.Invoke();
        }

        public bool TrySpendMp(int amount)
        {
            if (Mp < amount) return false;
            Mp -= amount;
            VitalsChanged?.Invoke();
            return true;
        }

        public IEnumerable<ItemDto> Items => Inventory?.Items ?? Enumerable.Empty<ItemDto>();

        public ItemDto FindItem(string templateId) => Items.FirstOrDefault(i => i.TemplateId == templateId);

        public int CountOf(string templateId) => Items.Where(i => i.TemplateId == templateId).Sum(i => i.Quantity);

        /// <summary>HP 회복 물약 중 가장 작은 것(자동 사용·퀵슬롯 기본값).</summary>
        public ItemDto BestHpPotion() =>
            FindItem(ItemCatalog.PotionHpSmall) ?? FindItem(ItemCatalog.PotionHpLarge);

        private void ClampVitals()
        {
            Hp = Math.Min(Hp, MaxHp);
            Mp = Math.Min(Mp, MaxMp);
            VitalsChanged?.Invoke();
        }
    }
}
