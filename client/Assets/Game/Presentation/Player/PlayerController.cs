// 내 캐릭터 조작: 조이스틱 이동, 대상 추적·기본 공격, 스킬, 물약, 줍기·NPC 접근, 귀환·부활
using System.Collections.Generic;
using System.Linq;
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class PlayerController : MonoBehaviour
    {
        public Actor Actor { get; private set; }
        public ActorMotor Motor { get; private set; }
        public Actor Target { get; private set; }
        public bool PkMode { get; set; }
        public bool AutoHunt { get; set; }
        public Vector2 JoystickInput { get; set; }

        public ClassCombatProfile Profile { get; private set; }
        public float AttackCooldown { get; private set; }
        public readonly float[] SkillCooldowns = new float[2];
        public float PotionCooldown { get; private set; }

        private GroundItemView _pickupTarget;
        private NpcController _npcTarget;
        private bool _potionBusy;
        private PlayerState Player => GameContext.Player;

        public event System.Action TargetChanged;

        public static PlayerController Spawn(CharacterDto character, Transform parent)
        {
            var root = new GameObject("Player");
            root.transform.SetParent(parent, false);
            var visual = CharacterVisual.Attach(root, GameResources.Prefab(GameResources.CharacterKey(character.ClassType, character.Gender)),
                new Color(0.3f, 0.6f, 1f));
            var actor = root.AddComponent<Actor>();
            actor.Visual = visual;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.5f;
            var motor = root.AddComponent<ActorMotor>();
            motor.Speed = CombatRules.PlayerMoveSpeed;

            var controller = root.AddComponent<PlayerController>();
            controller.Actor = actor;
            controller.Motor = motor;
            controller.Profile = CombatRules.ProfileOf(character.ClassType);
            actor.Init(Team.Player, character.Name, character.Level, character.Stats.MaxHp, character.Stats.Attack, character.Stats.Defense);
            motor.Teleport(new Vector3(character.Position.X, 0, character.Position.Z));
            actor.Damaged += controller.OnDamaged;
            actor.Died += controller.OnDied;
            controller.Player.CharacterChanged += controller.SyncFromState;
            controller.Player.VitalsChanged += controller.SyncFromState;
            controller.SyncFromState();
            return controller;
        }

        private void OnDestroy()
        {
            if (Player == null) return;
            Player.CharacterChanged -= SyncFromState;
            Player.VitalsChanged -= SyncFromState;
        }

        /// <summary>서버 상태(PlayerState)를 전투 개체에 반영한다.</summary>
        private void SyncFromState()
        {
            var c = Player.Character;
            if (c == null || Actor == null) return;
            Actor.DisplayName = c.Name;
            Actor.Level = c.Level;
            Actor.MaxHp = Player.MaxHp;
            Actor.Hp = Player.Hp;
            Actor.Attack = c.Stats.Attack;
            Actor.Defense = c.Stats.Defense;
            Actor.Alignment = AlignmentRules.GetState(c.Alignment.Value, c.Alignment.PurpleUntil, System.DateTime.UtcNow);
        }

        private void OnDamaged(Actor attacker, int amount)
        {
            Player.Damage(amount);
            if (attacker != null && attacker.Team == Team.Bot && attacker.TryGetComponent<BotController>(out var bot))
                WorldServices.Reporter.ReportBotAttackedPlayer(bot);
        }

        private void OnDied(Actor killer)
        {
            ClearTarget();
            AutoHunt = false;
            WorldServices.Reporter.ReportPlayerDeath(killer);
        }

        private void Update()
        {
            if (Actor.IsDead) return;
            var dt = Time.deltaTime;
            AttackCooldown = Mathf.Max(0, AttackCooldown - dt);
            PotionCooldown = Mathf.Max(0, PotionCooldown - dt);
            for (var i = 0; i < SkillCooldowns.Length; i++) SkillCooldowns[i] = Mathf.Max(0, SkillCooldowns[i] - dt);
            // 선공 표시는 시간이 지나면 풀리므로 주기적으로 다시 계산한다.
            if (Time.frameCount % 30 == 0) SyncFromState();

            var input = JoystickInput;
#if UNITY_EDITOR
            input += new Vector2(UnityEngine.Input.GetAxisRaw("Horizontal"), UnityEngine.Input.GetAxisRaw("Vertical"));
#endif
            if (input.sqrMagnitude > 0.02f)
            {
                // 직접 움직이면 접근 중이던 행동을 취소한다(대상 선택은 유지).
                _pickupTarget = null;
                _npcTarget = null;
                Motor.MoveDirection(new Vector3(input.x, 0, input.y));
                return;
            }

            if (_pickupTarget != null)
            {
                if (Motor.MoveTowards(_pickupTarget.transform.position, 1.2f))
                {
                    WorldServices.Ground.PlayerPickup(_pickupTarget);
                    _pickupTarget = null;
                }
                return;
            }

            if (_npcTarget != null)
            {
                if (Motor.MoveTowards(_npcTarget.transform.position, WorldLayout.InteractRange - 1f))
                {
                    Motor.Face(_npcTarget.transform.position);
                    _npcTarget.Interact();
                    _npcTarget = null;
                }
                return;
            }

            if (Target != null)
            {
                if (Target.IsDead || !Target.isActiveAndEnabled)
                {
                    ClearTarget();
                    return;
                }
                if (!CanAttack(Target, out _)) return;
                if (!Motor.MoveTowards(Target.transform.position, Profile.AttackRange - 0.3f)) return;
                Motor.Face(Target.transform.position);
                if (AttackCooldown <= 0) BasicAttack();
            }
        }

        public void SetTarget(Actor actor)
        {
            Target = actor;
            NameplateLayer.Highlighted = actor;
            _pickupTarget = null;
            _npcTarget = null;
            if (actor != null && !CanAttack(actor, out var reason) && reason != null) Toast.Show(reason);
            TargetChanged?.Invoke();
        }

        public void ClearTarget()
        {
            if (Target == null) return;
            Target = null;
            NameplateLayer.Highlighted = null;
            TargetChanged?.Invoke();
        }

        public void WalkToPickup(GroundItemView item)
        {
            ClearTarget();
            _npcTarget = null;
            _pickupTarget = item;
        }

        public void WalkToNpc(NpcController npc)
        {
            ClearTarget();
            _pickupTarget = null;
            _npcTarget = npc;
        }

        /// <summary>공격 가능 여부. 불가하면 reason 에 안내 문구(조용히 무시할 때는 null).</summary>
        public bool CanAttack(Actor target, out string reason)
        {
            reason = null;
            if (target == null || target.IsDead) return false;
            switch (target.Team)
            {
                case Team.Monster:
                    return true;
                case Team.Bot:
                    if (!PkMode)
                    {
                        reason = "PK 모드를 켜야 다른 캐릭터를 공격할 수 있습니다.";
                        return false;
                    }
                    if (Actor.InSafeZone || target.InSafeZone)
                    {
                        reason = ErrorMessages.For(ErrorCodes.SafeZone);
                        return false;
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void BasicAttack()
        {
            AttackCooldown = Profile.AttackInterval;
            Actor.Visual.Attack();
            DealDamage(Target, 1f);
        }

        private void DealDamage(Actor target, float multiplier)
        {
            var damage = CombatRules.Damage(Actor.Attack, target.Defense, multiplier, Random.value);
            if (target.Team == Team.Bot && target.TryGetComponent<BotController>(out var bot)) WorldServices.Reporter.ReportPlayerAttackedBot(bot);
            target.TakeDamage(Actor, damage);
            EffectPlayer.Play(EffectPlayer.Hit, target.transform.position + Vector3.up, 1.5f);
        }

        public void UseSkill(int index)
        {
            if (Actor.IsDead) return;
            var skill = index == 0 ? Profile.Skill1 : Profile.Skill2;
            if (SkillCooldowns[index] > 0)
            {
                Toast.Show($"{skill.Name} 재사용 대기 중입니다.");
                return;
            }
            if (skill.NeedsTarget)
            {
                if (Target == null || !CanAttack(Target, out _))
                {
                    Toast.Show("공격할 대상을 먼저 선택하세요.");
                    return;
                }
                if (Actor.DistanceTo(Target) > Profile.AttackRange + 0.5f)
                {
                    Toast.Show("대상이 너무 멀리 있습니다.");
                    return;
                }
            }
            if (!Player.TrySpendMp(skill.MpCost))
            {
                Toast.Show("MP가 부족합니다.");
                return;
            }
            SkillCooldowns[index] = skill.Cooldown;

            if (skill.IsHeal)
            {
                Actor.Visual.Cast();
                var amount = Mathf.RoundToInt(Player.MaxHp * skill.HealPercent);
                Player.Heal(amount, 0);
                NameplateLayer.Popup(Actor, "+" + amount, UiColors.Good);
                EffectPlayer.Play(EffectPlayer.Heal, transform.position);
                return;
            }

            Actor.Visual.Attack(skill: true);
            if (Target != null) Motor.Face(Target.transform.position);
            if (skill.Radius > 0)
            {
                var center = skill.CenteredOnSelf || Target == null ? Actor.Flat : Target.Flat;
                EffectPlayer.Play(EffectPlayer.Skill, new Vector3(center.X, transform.position.y, center.Z));
                var victims = Actor.All.Where(a => !a.IsDead && Vec2.Distance(a.Flat, center) <= skill.Radius && CanAttack(a, out _)).ToList();
                foreach (var victim in victims) DealDamage(victim, skill.Multiplier);
            }
            else
            {
                EffectPlayer.Play(EffectPlayer.Skill, Target.transform.position);
                DealDamage(Target, skill.Multiplier);
            }
        }

        /// <summary>HP 물약을 마신다. 소모는 서버(use-item)가 판정한다.</summary>
        public void UsePotion()
        {
            if (Actor.IsDead || _potionBusy || PotionCooldown > 0) return;
            var potion = Player.BestHpPotion();
            if (potion == null)
            {
                Toast.Show("물약이 없습니다.");
                return;
            }
            _potionBusy = true;
            PotionCooldown = CombatRules.PotionCooldown;
            AsyncUi.Run(async () =>
            {
                try
                {
                    var result = await GameContext.Api.UseItemAsync(potion.Id);
                    Player.Apply(result.Inventory);
                    Player.Heal(result.HealHp, result.HealMp);
                    if (this != null && result.HealHp > 0) NameplateLayer.Popup(Actor, "+" + result.HealHp, UiColors.Good);
                }
                finally
                {
                    _potionBusy = false;
                }
            });
        }

        public void ReturnToTown()
        {
            if (Actor.IsDead) return;
            ClearTarget();
            AutoHunt = false;
            TeleportTo(WorldLayout.RespawnPoint);
            Toast.Show("마을로 돌아왔습니다.");
        }

        public void TeleportTo(Vec2 point)
        {
            _pickupTarget = null;
            _npcTarget = null;
            Motor.Teleport(new Vector3(point.X, 0, point.Z));
            WorldServices.PositionSaver.SaveNow();
        }

        public void RespawnInTown()
        {
            Player.RestoreVitals();
            Actor.Revive();
            SyncFromState();
            Motor.Teleport(new Vector3(WorldLayout.RespawnPoint.X, 0, WorldLayout.RespawnPoint.Z));
        }

        public IEnumerable<Actor> AttackableAround(float radius) =>
            Actor.All.Where(a => a.Team == Team.Monster && !a.IsDead && a.isActiveAndEnabled && Actor.DistanceTo(a) <= radius);
    }
}
