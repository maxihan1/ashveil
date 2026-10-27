// 몬스터 개체: MonsterBrain(배회·어그로·추적·공격·리쉬) 명령을 이동·공격으로 실행
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class MonsterController : MonoBehaviour
    {
        private const float AttackerMemorySeconds = 10f;

        public SpawnPoint Spawn { get; private set; }
        public MonsterInfo Info { get; private set; }
        public Actor Actor { get; private set; }
        public MonsterState State => _brain.State;

        private MonsterBrain _brain;
        private ActorMotor _motor;

        public event System.Action<MonsterController, Actor> Killed;

        public static MonsterController Create(SpawnPoint spawn, Transform parent)
        {
            var info = spawn.Info;
            var root = new GameObject($"Monster_{spawn.Id}_{info.Type}");
            root.transform.SetParent(parent, false);
            var isBoss = info.Type == MonsterType.ImpBoss;
            var prefab = GameResources.Prefab(GameResources.MonsterKey(info.Type));
            // Boss Room 보스 모델은 원래 크다. 대체 캡슐일 때만 키운다.
            var visual = CharacterVisual.Attach(root, prefab,
                isBoss ? new Color(0.8f, 0.2f, 0.1f) : new Color(0.7f, 0.4f, 0.2f), isBoss && prefab == null ? 2.2f : 1f);
            var actor = root.AddComponent<Actor>();
            actor.Visual = visual;
            actor.NameplateHeight = visual.Height + 0.4f;
            actor.Init(Team.Monster, info.Name, info.Level, info.MaxHp, info.Attack, info.Defense);
            // 탭하기 쉽도록 모델 크기에 맞춘 넉넉한 콜라이더
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.height = Mathf.Max(1.6f, visual.Height);
            collider.center = Vector3.up * collider.height * 0.5f;
            collider.radius = Mathf.Max(0.6f, visual.Height * 0.3f);
            var motor = root.AddComponent<ActorMotor>();
            motor.Speed = info.MoveSpeed;

            var controller = root.AddComponent<MonsterController>();
            controller.Spawn = spawn;
            controller.Info = info;
            controller.Actor = actor;
            controller._motor = motor;
            controller._brain = new MonsterBrain(info, new Vec2(spawn.X, spawn.Z), spawn.Id.GetHashCode());
            actor.Died += controller.OnDied;
            motor.Teleport(new Vector3(spawn.X, 0, spawn.Z));
            return controller;
        }

        private void Update()
        {
            if (Actor.IsDead) return;
            var target = ChooseTarget();
            var senses = new MonsterSenses
            {
                Position = Actor.Flat,
                HasTarget = target != null,
                TargetPosition = target != null ? target.Flat : default,
                TargetInSafeZone = target != null && target.InSafeZone,
                WasHit = target != null && Actor.RecentlyHitBy(target, 0.5f),
            };
            var command = _brain.Tick(Time.deltaTime, senses);

            if (_brain.State == MonsterState.Return) Actor.Hp = Actor.MaxHp; // 끌려갔다 돌아가면 체력을 회복한다

            switch (command.Kind)
            {
                case MonsterCommandKind.MoveTo:
                    _motor.MoveTowards(command.Destination, 0.3f, _brain.State == MonsterState.Return ? 1.6f : 1f);
                    break;
                case MonsterCommandKind.Attack:
                    if (target == null) break;
                    _motor.Face(target.transform.position);
                    Actor.Visual.Attack(skill: Info.Type == MonsterType.ImpBoss && Random.value < 0.3f);
                    target.TakeDamage(Actor, CombatRules.Damage(Actor.Attack, target.Defense, 1f, Random.value));
                    break;
            }
        }

        /// <summary>최근에 때린 개체(플레이어·봇)를 우선, 없으면 플레이어.</summary>
        private Actor ChooseTarget()
        {
            var attacker = Actor.LastAttacker;
            if (attacker != null && !attacker.IsDead && attacker.isActiveAndEnabled && Time.time - Actor.LastHitTime < AttackerMemorySeconds)
                return attacker;
            var player = WorldServices.Player;
            return player != null && !player.Actor.IsDead ? player.Actor : null;
        }

        private void OnDied(Actor killer)
        {
            _brain.Kill();
            GetComponent<Collider>().enabled = false;
            Killed?.Invoke(this, killer);
        }

        public void Respawn()
        {
            _brain.Revive();
            _motor.Teleport(new Vector3(Spawn.X, 0, Spawn.Z));
            Actor.Revive();
            GetComponent<Collider>().enabled = true;
            gameObject.SetActive(true);
        }
    }
}
