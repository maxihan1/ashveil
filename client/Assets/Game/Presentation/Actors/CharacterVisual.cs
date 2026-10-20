// Boss Room 그래픽 프리팹(또는 대체 도형)을 붙이고 애니메이터 파라미터를 안전하게 구동
using System.Collections.Generic;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class CharacterVisual : MonoBehaviour
    {
        // Boss Room CharacterSetController 파라미터 이름
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Attack1 = Animator.StringToHash("Attack1");
        private static readonly int Attack2 = Animator.StringToHash("Attack2");
        private static readonly int HitReact = Animator.StringToHash("HitReact1");
        private static readonly int Dead = Animator.StringToHash("Dead");
        private static readonly int FallDown = Animator.StringToHash("FallDown");
        private static readonly int StandUp = Animator.StringToHash("StandUp");
        private static readonly int SkillHeal = Animator.StringToHash("SkillHeal");

        private Animator _animator;
        private readonly HashSet<int> _parameters = new HashSet<int>();
        private Transform _model;
        private bool _moving;

        public static CharacterVisual Attach(GameObject root, GameObject prefab, Color fallbackColor, float scale = 1f)
        {
            GameObject model;
            if (prefab != null)
            {
                model = Instantiate(prefab, root.transform);
            }
            else
            {
                model = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(model.GetComponent<Collider>());
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.up;
                var material = GameResources.FallbackMaterial("FallbackActor");
                var renderer = model.GetComponent<Renderer>();
                if (material != null) renderer.sharedMaterial = material;
                renderer.material.color = fallbackColor;
            }
            model.name = "Visual";
            model.transform.localRotation = Quaternion.identity;
            // Boss Room 은 몬스터 크기를 그래픽 루트 배율로 둔다(임프 0.6, 보스 3). 덮어쓰지 말고 곱한다.
            model.transform.localScale *= scale;
            if (prefab == null) model.transform.localPosition = Vector3.up * scale; // 캡슐 바닥을 지면에 맞춘다

            var visual = root.AddComponent<CharacterVisual>();
            visual._model = model.transform;
            visual.Height = MeasureHeight(root.transform, model);
            visual._animator = model.GetComponentInChildren<Animator>();
            if (visual._animator != null)
            {
                visual._animator.applyRootMotion = false;
                visual._animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (visual._animator.runtimeAnimatorController != null)
                    foreach (var p in visual._animator.parameters) visual._parameters.Add(p.nameHash);
                if (visual._animator.GetComponent<AnimEventReceiver>() == null) visual._animator.gameObject.AddComponent<AnimEventReceiver>();
            }
            return visual;
        }

        /// <summary>모델 높이(m). 이름표 위치·콜라이더 크기를 맞출 때 쓴다.</summary>
        public float Height { get; private set; } = 2f;

        private static float MeasureHeight(Transform root, GameObject model)
        {
            var top = float.MinValue;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                if (!(r is ParticleSystemRenderer)) top = Mathf.Max(top, r.bounds.max.y);
            var height = top - root.position.y;
            return top > float.MinValue && height > 0.3f && height < 20f ? height : 2f;
        }

        // 거리 컬링으로 꺼졌다 켜지면 Animator 파라미터가 초기화되므로 기억한 이동 상태도 맞춘다.
        private void OnEnable() => _moving = false;

        public void SetMoving(bool moving)
        {
            if (moving == _moving) return;
            _moving = moving;
            if (_animator != null && _parameters.Contains(Speed)) _animator.SetFloat(Speed, moving ? 1f : 0f);
        }

        public void Attack(bool skill = false) => Trigger(skill && _parameters.Contains(Attack2) ? Attack2 : Attack1);

        public void Cast() => Trigger(_parameters.Contains(SkillHeal) ? SkillHeal : Attack2);

        public void Hit() => Trigger(HitReact);

        public void Die()
        {
            Trigger(_parameters.Contains(Dead) ? Dead : FallDown);
            if (_animator == null && _model != null) _model.localRotation = Quaternion.Euler(90, 0, 0);
        }

        public void Revive()
        {
            if (_animator != null)
            {
                // 사망 상태 애니메이션에서 빠져나오도록 처음 상태로 되돌린다.
                _animator.Rebind();
                _animator.Update(0f);
                _moving = false;
                Trigger(StandUp);
            }
            else if (_model != null)
            {
                _model.localRotation = Quaternion.identity;
            }
        }

        private void Trigger(int hash)
        {
            if (_animator != null && _parameters.Contains(hash)) _animator.SetTrigger(hash);
        }
    }
}
