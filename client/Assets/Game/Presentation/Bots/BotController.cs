// 봇 개체: 서버 봇 데이터(이름·레벨·장비·성향) + BotBrain 행동(배회·사냥·PK·도망·줍기·마을 방문)
using System.Linq;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class BotController : MonoBehaviour
    {
        private const float DecideSeconds = 0.3f;
        private const float AttackedMemorySeconds = 15f;
        private const float LeashFromZone = 30f;

        public BotDto Data { get; private set; }
        public Actor Actor { get; private set; }
        public BotActionKind CurrentAction { get; private set; } = BotActionKind.Wander;

        private ActorMotor _motor;
        private ClassCombatProfile _profile;
        private float _decideTimer;
        private float _attackTimer;
        private float _attackedByPlayerUntil;
        private Vector3 _wanderTarget;
        private float _wanderTimer;
        private float _nextTownVisit;
        private float _townStayUntil;
        private Actor _monsterTarget;
        private GroundItemView _itemTarget;
        private bool _pickupInFlight;
        private bool _deathReported;

        public bool LocallyDead { get; private set; }

        public static BotController Create(BotDto data, Transform parent)
        {
            var root = new GameObject($"Bot_{data.Id}_{data.Name}");
            root.transform.SetParent(parent, false);
            var visual = CharacterVisual.Attach(root, GameResources.Prefab(GameResources.CharacterKey(data.ClassType, data.Gender)),
                new Color(0.6f, 0.6f, 0.9f));
            var actor = root.AddComponent<Actor>();
            actor.Visual = visual;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.5f;
            var motor = root.AddComponent<ActorMotor>();
            motor.Speed = CombatRules.BotMoveSpeed;

            var bot = root.AddComponent<BotController>();
            bot.Actor = actor;
            bot._motor = motor;
            bot._profile = CombatRules.ProfileOf(data.ClassType);
            bot._nextTownVisit = Time.time + Random.Range(120f, 300f);
            actor.Damaged += bot.OnDamaged;
            actor.Died += bot.OnDied;
            bot.Apply(data, true);
            return bot;
        }

        /// <summary>서버 데이터 반영. 사망·부활 상태가 바뀌면 숨기거나 출현 지점에 다시 세운다.</summary>
        public void Apply(BotDto data, bool first = false)
        {
            Data = data;
            var stats = StatRules.Compute(data.ClassType, data.Level, data.Equipment.Select(e => new EquippedItem(e.TemplateId, e.EnhanceLevel)));
            var wasDead = LocallyDead;
            Actor.DisplayName = data.Name;
            Actor.Level = data.Level;
            Actor.Attack = stats.Attack;
            Actor.Defense = stats.Defense;
            Actor.MaxHp = stats.MaxHp;
            ApplyAlignment(data.Alignment);

            if (data.IsDead)
            {
                LocallyDead = true;
                if (first || !wasDead) gameObject.SetActive(false);
                return;
            }
            if (first || wasDead)
            {
                LocallyDead = false;
                _deathReported = false;
                gameObject.SetActive(true);
                Actor.Revive();
                var spawn = BotBrain.SpawnPoint(data.Id, data.Zone, Random.Range(0, 1000));
                _motor.Teleport(new Vector3(spawn.X, 0, spawn.Z));
                _wanderTarget = transform.position;
            }
        }

        public void ApplyAlignment(AlignmentDto alignment)
        {
            if (alignment == null) return;
            Data.Alignment = alignment;
            Actor.Alignment = AlignmentRules.GetState(alignment.Value, alignment.PurpleUntil, System.DateTime.UtcNow);
        }

        private void OnDamaged(Actor attacker, int amount)
        {
            if (attacker != null && attacker.Team == Team.Player) _attackedByPlayerUntil = Time.time + AttackedMemorySeconds;
        }

        private void OnDied(Actor killer)
        {
            if (_deathReported) return;
            _deathReported = true;
            LocallyDead = true;
            if (killer != null && killer.Team == Team.Player) WorldServices.Reporter.ReportBotKill(this, killer.transform.position);
            Invoke(nameof(HideCorpse), 2.5f);
        }

        private void HideCorpse()
        {
            if (LocallyDead) gameObject.SetActive(false);
        }

        private void Update()
        {
            if (Actor.IsDead || LocallyDead) return;
            var dt = Time.deltaTime;
            _attackTimer -= dt;
            _decideTimer -= dt;
            if (Time.frameCount % 30 == 0) ApplyAlignment(Data.Alignment);
            if (_decideTimer <= 0)
            {
                _decideTimer = DecideSeconds;
                CurrentAction = Decide();
            }
            Act(dt);
        }

        private BotActionKind Decide()
        {
            var player = WorldServices.Player;
            _monsterTarget = Actor.Nearest(Actor.Flat, BotBrain.MonsterSightRange, a => a.Team == Team.Monster && a.isActiveAndEnabled);
            _itemTarget = _pickupInFlight ? _itemTarget : WorldServices.Ground?.NearestFor(Data.Zone, Actor.Flat, BotBrain.ItemSightRange);
            var senses = new BotSenses
            {
                Position = Actor.Flat,
                HpRatio = (float)Actor.Hp / Actor.MaxHp,
                InSafeZone = Actor.InSafeZone,
                PlayerVisible = player != null,
                PlayerPosition = player != null ? player.Actor.Flat : default,
                PlayerState = player != null ? player.Actor.Alignment : AlignmentState.Innocent,
                PlayerInSafeZone = player != null && player.Actor.InSafeZone,
                PlayerDead = player == null || player.Actor.IsDead,
                AttackedByPlayerRecently = Time.time < _attackedByPlayerUntil,
                MonsterVisible = _monsterTarget != null,
                MonsterPosition = _monsterTarget != null ? _monsterTarget.Flat : default,
                GroundItemVisible = _itemTarget != null,
                GroundItemPosition = _itemTarget != null ? WorldGround.Flat(_itemTarget.transform.position) : default,
                TownVisitDue = Time.time >= _nextTownVisit,
            };
            var action = BotBrain.Decide(Data.Temperament, Actor.Alignment, senses);
            // 자기 구역에서 너무 멀어지면 사냥터로 돌아간다(마을 방문 중 제외).
            if (action != BotActionKind.GoToTown && action != BotActionKind.AttackPlayer && action != BotActionKind.Flee && !NearOwnZone())
                return BotActionKind.Wander;
            return action;
        }

        private bool NearOwnZone()
        {
            var b = ZoneMap.BoundsOf(Data.Zone);
            var p = transform.position;
            return p.x > b.MinX - LeashFromZone && p.x < b.MaxX + LeashFromZone && p.z > b.MinZ - LeashFromZone && p.z < b.MaxZ + LeashFromZone;
        }

        private void Act(float dt)
        {
            var player = WorldServices.Player;
            switch (CurrentAction)
            {
                case BotActionKind.AttackPlayer:
                    if (player == null) break;
                    if (_motor.MoveTowards(player.transform.position, _profile.AttackRange - 0.3f))
                    {
                        _motor.Face(player.transform.position);
                        if (_attackTimer <= 0) Strike(player.Actor);
                    }
                    break;

                case BotActionKind.Flee:
                    if (player == null) break;
                    var away = (transform.position - player.transform.position).normalized;
                    _motor.MoveDirection(away);
                    break;

                case BotActionKind.HuntMonster:
                    if (_monsterTarget == null || _monsterTarget.IsDead) break;
                    if (_motor.MoveTowards(_monsterTarget.transform.position, _profile.AttackRange - 0.3f))
                    {
                        _motor.Face(_monsterTarget.transform.position);
                        if (_attackTimer <= 0) Strike(_monsterTarget);
                    }
                    break;

                case BotActionKind.PickupItem:
                    if (_itemTarget == null) break;
                    if (_motor.MoveTowards(_itemTarget.transform.position, 1.2f) && !_pickupInFlight) TryPickup(_itemTarget);
                    break;

                case BotActionKind.GoToTown:
                    if (_townStayUntil <= 0)
                    {
                        if (_motor.MoveTowards(WorldLayout.TownPlaza, 6f)) _townStayUntil = Time.time + 20f;
                    }
                    else if (Time.time >= _townStayUntil)
                    {
                        _townStayUntil = 0;
                        _nextTownVisit = Time.time + Random.Range(180f, 360f);
                        _wanderTimer = 0;
                    }
                    break;

                case BotActionKind.Rest:
                    Actor.Heal(Mathf.CeilToInt(Actor.MaxHp * 0.03f * dt * 10f) / 10 + 1);
                    break;

                default:
                    Wander(dt);
                    break;
            }
        }

        private void Wander(float dt)
        {
            _wanderTimer -= dt;
            if (_wanderTimer <= 0 || _motor.MoveTowards(_wanderTarget, 0.5f))
            {
                if (_wanderTimer <= 0)
                {
                    _wanderTimer = Random.Range(5f, 9f);
                    var point = PickWanderPoint();
                    _wanderTarget = new Vector3(point.X, 0, point.Z);
                }
            }
        }

        private Vec2 PickWanderPoint()
        {
            for (var i = 0; i < 8; i++)
            {
                var offset = Random.insideUnitCircle * 15f;
                var x = Mathf.Clamp(transform.position.x + offset.x, 2f, ZoneMap.WorldSize - 2f);
                var z = Mathf.Clamp(transform.position.z + offset.y, 2f, ZoneMap.WorldSize - 2f);
                if (ZoneMap.GetZone(x, z) == Data.Zone) return new Vec2(x, z);
            }
            return BotBrain.SpawnPoint(Data.Id, Data.Zone, Random.Range(0, 1000));
        }

        private void Strike(Actor target)
        {
            _attackTimer = _profile.AttackInterval * 1.2f;
            Actor.Visual.Attack();
            target.TakeDamage(Actor, CombatRules.Damage(Actor.Attack, target.Defense, 1f, Random.value));
        }

        private void TryPickup(GroundItemView item)
        {
            _pickupInFlight = true;
            WorldServices.Ground.BotPickup(this, item, () =>
            {
                _pickupInFlight = false;
                _itemTarget = null;
            });
        }
    }
}
