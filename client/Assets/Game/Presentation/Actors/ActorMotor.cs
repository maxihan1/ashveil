// 지형을 따라 걷는 이동기 (목표 지점 이동, 방향 이동, 바라보기)
using Ashveil.Domain;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class ActorMotor : MonoBehaviour
    {
        public float Speed = 4f;
        public float TurnSpeed = 720f;
        private CharacterVisual _visual;
        private bool _movedThisFrame;

        public bool IsMoving { get; private set; }

        private void Awake() => _visual = GetComponent<CharacterVisual>();

        private void LateUpdate()
        {
            IsMoving = _movedThisFrame;
            if (_visual == null) _visual = GetComponent<CharacterVisual>();
            if (_visual != null) _visual.SetMoving(IsMoving);
            _movedThisFrame = false;
        }

        /// <summary>목표까지 한 프레임 이동한다. 도착(stopDistance 이내)하면 true.</summary>
        public bool MoveTowards(Vector3 target, float stopDistance, float speedMultiplier = 1f)
        {
            var position = transform.position;
            var delta = new Vector3(target.x - position.x, 0, target.z - position.z);
            var distance = delta.magnitude;
            if (distance <= stopDistance) return true;
            var step = Mathf.Min(distance - stopDistance * 0.5f, Speed * speedMultiplier * Time.deltaTime);
            Step(delta / distance, step);
            return false;
        }

        public bool MoveTowards(Vec2 target, float stopDistance, float speedMultiplier = 1f) =>
            MoveTowards(new Vector3(target.X, 0, target.Z), stopDistance, speedMultiplier);

        /// <summary>direction 은 XZ 평면 방향(길이 1 이하).</summary>
        public void MoveDirection(Vector3 direction)
        {
            var flat = new Vector3(direction.x, 0, direction.z);
            var magnitude = Mathf.Min(1f, flat.magnitude);
            if (magnitude < 0.05f) return;
            Step(flat.normalized, Speed * magnitude * Time.deltaTime);
        }

        public void Face(Vector3 target)
        {
            var dir = new Vector3(target.x - transform.position.x, 0, target.z - transform.position.z);
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }

        public void Teleport(Vector3 position)
        {
            transform.position = WorldGround.Snap(WorldGround.ClampToWorld(position));
        }

        private void Step(Vector3 direction, float distance)
        {
            var next = WorldGround.ClampToWorld(transform.position + direction * distance);
            transform.position = WorldGround.Snap(next);
            var look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, TurnSpeed * Time.deltaTime);
            _movedThisFrame = true;
        }
    }
}
