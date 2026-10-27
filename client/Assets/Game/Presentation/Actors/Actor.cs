// 플레이어·몬스터·봇·NPC 공통 전투 개체 (팀, HP, 공격·방어, 피격·사망 이벤트, 전역 목록)
using System;
using System.Collections.Generic;
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;

namespace Ashveil.Presentation
{
    public enum Team { Player, Monster, Bot, Guard, Npc }

    public sealed class Actor : MonoBehaviour
    {
        public static readonly List<Actor> All = new List<Actor>();

        public Team Team;
        public string DisplayName;
        public int Level;
        public int MaxHp = 1;
        public int Hp = 1;
        public int Attack;
        public int Defense;
        public bool Invulnerable;
        public float NameplateHeight = 2.3f;
        public AlignmentState Alignment = AlignmentState.Innocent;

        public Actor LastAttacker { get; private set; }
        public float LastHitTime { get; private set; } = -999f;
        public CharacterVisual Visual { get; set; }

        /// <summary>(공격자, 데미지)</summary>
        public event Action<Actor, int> Damaged;
        /// <summary>(처치자)</summary>
        public event Action<Actor> Died;

        public bool IsDead => Hp <= 0;
        public Vec2 Flat => new Vec2(transform.position.x, transform.position.z);
        public Vector3 HeadPosition => transform.position + Vector3.up * NameplateHeight;
        public bool InSafeZone => WorldGround.IsSafe(transform.position);

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        public void Init(Team team, string displayName, int level, int maxHp, int attack, int defense)
        {
            Team = team;
            DisplayName = displayName;
            Level = level;
            MaxHp = Mathf.Max(1, maxHp);
            Hp = MaxHp;
            Attack = attack;
            Defense = defense;
        }

        public void TakeDamage(Actor attacker, int amount)
        {
            if (IsDead || Invulnerable || amount <= 0) return;
            // 봇은 플레이어에게만 쓰러진다. 봇 사망은 서버(bot-kill)에 보고해야 하므로 다른 원인으로는 죽지 않게 한다.
            var floor = Team == Team.Bot && (attacker == null || attacker.Team != Team.Player) ? 1 : 0;
            Hp = Mathf.Max(floor, Hp - amount);
            LastAttacker = attacker;
            LastHitTime = Time.time;
            if (Visual != null) Visual.Hit();
            NameplateLayer.Popup(this, amount.ToString(), Team == Team.Player ? UiColors.Bad : Color.white);
            Damaged?.Invoke(attacker, amount);
            if (Hp <= 0)
            {
                if (Visual != null) Visual.Die();
                Died?.Invoke(attacker);
            }
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0) return;
            Hp = Mathf.Min(MaxHp, Hp + amount);
        }

        public void Revive()
        {
            Hp = MaxHp;
            LastAttacker = null;
            LastHitTime = -999f;
            if (Visual != null) Visual.Revive();
        }

        public bool RecentlyHitBy(Actor attacker, float seconds) =>
            LastAttacker == attacker && Time.time - LastHitTime <= seconds;

        public float DistanceTo(Actor other) => Vec2.Distance(Flat, other.Flat);

        /// <summary>조건에 맞는 가장 가까운 살아 있는 개체.</summary>
        public static Actor Nearest(Vec2 from, float radius, Func<Actor, bool> filter)
        {
            Actor best = null;
            var bestDistance = radius;
            foreach (var actor in All)
            {
                if (actor.IsDead || !filter(actor)) continue;
                var d = Vec2.Distance(from, actor.Flat);
                if (d <= bestDistance)
                {
                    best = actor;
                    bestDistance = d;
                }
            }
            return best;
        }
    }
}
