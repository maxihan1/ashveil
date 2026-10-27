// 몬스터 AI 상태기계: 배회 → 어그로 → 추적 → 공격 → 리쉬(복귀)
using System;
using Ashveil.Shared;

namespace Ashveil.Domain
{
    public enum MonsterState { Wander, Chase, Attack, Return, Dead }

    public enum MonsterCommandKind { Idle, MoveTo, Attack }

    public readonly struct MonsterCommand
    {
        public readonly MonsterCommandKind Kind;
        public readonly Vec2 Destination;

        public MonsterCommand(MonsterCommandKind kind, Vec2 destination)
        {
            Kind = kind;
            Destination = destination;
        }
    }

    /// <summary>한 프레임에 몬스터가 아는 바깥 정보.</summary>
    public struct MonsterSenses
    {
        public Vec2 Position;
        /// <summary>추적 대상이 있으면 true (플레이어·봇 중 가장 가까운 적).</summary>
        public bool HasTarget;
        public Vec2 TargetPosition;
        /// <summary>대상이 안전지대에 있으면 쫓지 않는다.</summary>
        public bool TargetInSafeZone;
        /// <summary>이번 프레임에 공격을 받았으면 true (어그로 반경 밖이어도 반응).</summary>
        public bool WasHit;
    }

    public sealed class MonsterBrain
    {
        private readonly MonsterInfo _info;
        private readonly Vec2 _home;
        private readonly Random _random;
        private Vec2 _wanderTarget;
        private float _wanderWait;
        private float _attackCooldown;

        public MonsterState State { get; private set; } = MonsterState.Wander;

        public MonsterBrain(MonsterInfo info, Vec2 home, int seed)
        {
            _info = info;
            _home = home;
            _random = new Random(seed);
            _wanderTarget = home;
        }

        public const float WanderRadius = 6f;

        public void Kill() => State = MonsterState.Dead;

        public void Revive()
        {
            State = MonsterState.Wander;
            _attackCooldown = 0;
            _wanderTarget = _home;
        }

        public MonsterCommand Tick(float deltaTime, MonsterSenses senses)
        {
            if (State == MonsterState.Dead) return new MonsterCommand(MonsterCommandKind.Idle, senses.Position);
            _attackCooldown = Math.Max(0f, _attackCooldown - deltaTime);

            var fromHome = Vec2.Distance(senses.Position, _home);
            if (State != MonsterState.Return && fromHome > _info.LeashRadius) State = MonsterState.Return;

            if (State == MonsterState.Return)
            {
                if (fromHome < 1.5f) State = MonsterState.Wander;
                else return new MonsterCommand(MonsterCommandKind.MoveTo, _home);
            }

            var canEngage = senses.HasTarget && !senses.TargetInSafeZone;
            var targetDistance = canEngage ? Vec2.Distance(senses.Position, senses.TargetPosition) : float.MaxValue;
            // 대상이 리쉬 경계 밖에 있으면 쫓아가지 않는다(경계에서 끌려다니는 것 방지).
            var targetFromHome = canEngage ? Vec2.Distance(senses.TargetPosition, _home) : float.MaxValue;
            var engaged = State == MonsterState.Chase || State == MonsterState.Attack;
            var aggro = canEngage && targetFromHome <= _info.LeashRadius &&
                        (targetDistance <= _info.AggroRadius || senses.WasHit || engaged);

            if (!aggro)
            {
                if (engaged) State = MonsterState.Return;
                else State = MonsterState.Wander;
                return State == MonsterState.Return
                    ? new MonsterCommand(MonsterCommandKind.MoveTo, _home)
                    : WanderStep(deltaTime, senses.Position);
            }

            if (targetDistance > _info.AttackRange)
            {
                State = MonsterState.Chase;
                return new MonsterCommand(MonsterCommandKind.MoveTo, senses.TargetPosition);
            }

            State = MonsterState.Attack;
            if (_attackCooldown > 0f) return new MonsterCommand(MonsterCommandKind.Idle, senses.Position);
            _attackCooldown = _info.AttackInterval;
            return new MonsterCommand(MonsterCommandKind.Attack, senses.TargetPosition);
        }

        private MonsterCommand WanderStep(float deltaTime, Vec2 position)
        {
            if (Vec2.Distance(position, _wanderTarget) < 0.5f)
            {
                _wanderWait -= deltaTime;
                if (_wanderWait > 0f) return new MonsterCommand(MonsterCommandKind.Idle, position);
                var angle = _random.NextDouble() * Math.PI * 2;
                var radius = _random.NextDouble() * WanderRadius;
                _wanderTarget = new Vec2(_home.X + (float)(Math.Cos(angle) * radius), _home.Z + (float)(Math.Sin(angle) * radius));
                _wanderWait = 2f + (float)_random.NextDouble() * 3f;
            }
            return new MonsterCommand(MonsterCommandKind.MoveTo, _wanderTarget);
        }
    }
}
