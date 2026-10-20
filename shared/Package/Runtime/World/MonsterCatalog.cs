// 몬스터 종류별 능력치·보상·AI 수치 카탈로그
using System.Collections.Generic;

namespace Ashveil.Shared
{
    public sealed class MonsterInfo
    {
        public MonsterType Type { get; }
        public string Name { get; }
        public int Level { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }
        public long Exp { get; }
        public int GoldMin { get; }
        public int GoldMax { get; }
        public int RespawnSeconds { get; }
        public float LeashRadius { get; }
        public float AggroRadius { get; }
        public float AttackRange { get; }
        public float MoveSpeed { get; }
        public float AttackInterval { get; }

        public MonsterInfo(MonsterType type, string name, int level, int maxHp, int attack, int defense, long exp,
            int goldMin, int goldMax, int respawnSeconds, float leashRadius, float aggroRadius, float attackRange,
            float moveSpeed, float attackInterval)
        {
            Type = type; Name = name; Level = level; MaxHp = maxHp; Attack = attack; Defense = defense; Exp = exp;
            GoldMin = goldMin; GoldMax = goldMax; RespawnSeconds = respawnSeconds; LeashRadius = leashRadius;
            AggroRadius = aggroRadius; AttackRange = attackRange; MoveSpeed = moveSpeed; AttackInterval = attackInterval;
        }
    }

    public static class MonsterCatalog
    {
        private static readonly Dictionary<MonsterType, MonsterInfo> Monsters = new Dictionary<MonsterType, MonsterInfo>
        {
            [MonsterType.Imp] = new MonsterInfo(MonsterType.Imp, "임프", 3, 60, 8, 2, 20, 8, 15, 20, 25f, 8f, 1.8f, 3.2f, 1.6f),
            [MonsterType.VandalImp] = new MonsterInfo(MonsterType.VandalImp, "반달 임프", 10, 220, 22, 8, 90, 30, 55, 25, 25f, 8f, 2.0f, 3.6f, 1.5f),
            [MonsterType.ImpBoss] = new MonsterInfo(MonsterType.ImpBoss, "임프 군주", 20, 3000, 60, 20, 2000, 800, 1200, 300, 30f, 12f, 3.5f, 3.0f, 2.2f),
        };

        public static MonsterInfo Get(MonsterType type) => Monsters[type];

        public static IEnumerable<MonsterInfo> All => Monsters.Values;
    }
}
