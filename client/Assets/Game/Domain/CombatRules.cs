// 클라이언트 전투 계산: 데미지 공식, 직업별 사거리·공격 간격, 스킬 정의
using System;
using Ashveil.Shared;

namespace Ashveil.Domain
{
    public sealed class SkillDef
    {
        public string Id { get; }
        public string Name { get; }
        public int MpCost { get; }
        public float Cooldown { get; }
        /// <summary>공격력 배율. 회복 스킬이면 0.</summary>
        public float Multiplier { get; }
        /// <summary>0보다 크면 범위 공격 반경(m).</summary>
        public float Radius { get; }
        /// <summary>회복 스킬이면 최대 HP 대비 회복 비율.</summary>
        public float HealPercent { get; }
        /// <summary>범위 공격 중심이 자신이면 true, 대상이면 false.</summary>
        public bool CenteredOnSelf { get; }

        public SkillDef(string id, string name, int mpCost, float cooldown, float multiplier,
            float radius = 0f, float healPercent = 0f, bool centeredOnSelf = false)
        {
            Id = id; Name = name; MpCost = mpCost; Cooldown = cooldown; Multiplier = multiplier;
            Radius = radius; HealPercent = healPercent; CenteredOnSelf = centeredOnSelf;
        }

        public bool IsHeal => HealPercent > 0f;
        public bool NeedsTarget => !IsHeal && !CenteredOnSelf;
    }

    public sealed class ClassCombatProfile
    {
        public float AttackRange { get; }
        public float AttackInterval { get; }
        public SkillDef Skill1 { get; }
        public SkillDef Skill2 { get; }

        public ClassCombatProfile(float attackRange, float attackInterval, SkillDef skill1, SkillDef skill2)
        {
            AttackRange = attackRange; AttackInterval = attackInterval; Skill1 = skill1; Skill2 = skill2;
        }
    }

    public static class CombatRules
    {
        public const float PlayerMoveSpeed = 6f;
        public const float BotMoveSpeed = 4.5f;
        public const float PotionCooldown = 1.5f;
        public const float RegenPerSecondPercent = 0.01f;

        private static readonly ClassCombatProfile Warrior = new ClassCombatProfile(2.6f, 1.0f,
            new SkillDef("warrior_strike", "강타", 10, 6f, 2.0f),
            new SkillDef("warrior_cleave", "대지 가르기", 20, 10f, 1.2f, radius: 4f, centeredOnSelf: true));

        private static readonly ClassCombatProfile Archer = new ClassCombatProfile(12f, 1.1f,
            new SkillDef("archer_aimed", "조준 사격", 10, 6f, 2.2f),
            new SkillDef("archer_rain", "화살비", 22, 12f, 1.0f, radius: 5f));

        private static readonly ClassCombatProfile Mage = new ClassCombatProfile(10f, 1.2f,
            new SkillDef("mage_fireball", "화염구", 12, 5f, 2.5f),
            new SkillDef("mage_heal", "회복", 25, 12f, 0f, healPercent: 0.35f));

        public static ClassCombatProfile ProfileOf(ClassType classType)
        {
            switch (classType)
            {
                case ClassType.Archer: return Archer;
                case ClassType.Mage: return Mage;
                default: return Warrior;
            }
        }

        /// <summary>데미지 = max(1, 공격력 − 방어력/2) × 배율 × 무작위(0.9~1.1).</summary>
        public static int Damage(int attack, int defense, float multiplier, double random01)
        {
            var baseDamage = Math.Max(1.0, attack - defense / 2.0);
            var variance = 0.9 + 0.2 * random01;
            return Math.Max(1, (int)Math.Round(baseDamage * multiplier * variance));
        }
    }
}
