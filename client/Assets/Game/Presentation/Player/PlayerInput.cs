// 쿼터뷰 추적 카메라, 가상 조이스틱, 화면 탭 타겟팅(몬스터·봇·NPC·바닥 아이템)
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(0, 13f, -10.5f);
        public float Smooth = 10f;

        public static CameraRig Create(Transform target)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                camera = go.GetComponent<Camera>();
            }
            camera.farClipPlane = 140f;
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 45f;
            // GetComponent 결과에 ?? 를 쓰면 에디터의 가짜 null 을 통과시키므로 == null 로 확인한다.
            var rig = camera.GetComponent<CameraRig>();
            if (rig == null) rig = camera.gameObject.AddComponent<CameraRig>();
            rig.Target = target;
            rig.Snap();
            return rig;
        }

        public void Snap()
        {
            if (Target == null) return;
            transform.position = Target.position + Offset;
            transform.rotation = Quaternion.LookRotation(-Offset.normalized);
        }

        private void LateUpdate()
        {
            if (Target == null) return;
            var desired = Target.position + Offset;
            if ((desired - transform.position).sqrMagnitude > 400f) transform.position = desired; // 순간이동 후
            else transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-Smooth * Time.deltaTime));
            transform.rotation = Quaternion.LookRotation(-Offset.normalized);
        }
    }

    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        private RectTransform _area;
        private RectTransform _knob;
        private float _radius;

        public Vector2 Value { get; private set; }

        public static VirtualJoystick Create(Transform parent)
        {
            var area = Ui.Panel(parent, "Joystick", new Color(1, 1, 1, 0.12f));
            area.sprite = Ui.Circle;
            Ui.Place(area.rectTransform, Vector2.zero, Vector2.zero, new Vector2(60, 60), new Vector2(340, 340));
            var knob = Ui.Panel(area.transform, "Knob", new Color(1, 1, 1, 0.45f));
            knob.sprite = Ui.Circle;
            knob.raycastTarget = false;
            Ui.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            var joystick = area.gameObject.AddComponent<VirtualJoystick>();
            joystick._area = area.rectTransform;
            joystick._knob = knob.rectTransform;
            joystick._radius = 120f;
            return joystick;
        }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        // 조이스틱은 드래그 임계값 없이 바로 반응한다(EventSystem 임계값은 스크롤 목록용으로 크게 잡는다).
        public void OnInitializePotentialDrag(PointerEventData eventData) => eventData.useDragThreshold = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, eventData.position, eventData.pressEventCamera, out var local)) return;
            // 로컬 좌표는 pivot(왼쪽 아래) 기준이므로 패드 중심 기준으로 옮긴다.
            local -= _area.rect.center;
            var clamped = Vector2.ClampMagnitude(local, _radius);
            _knob.anchoredPosition = clamped;
            Value = clamped / _radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _knob.anchoredPosition = Vector2.zero;
            Value = Vector2.zero;
        }

        private void OnDisable()
        {
            Value = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>UI 가 아닌 곳을 탭하면 레이캐스트로 대상을 고른다.</summary>
    public sealed class TapTargeting : MonoBehaviour
    {
        private const float TapMaxMove = 30f;
        private readonly Dictionary<int, (Vector2 start, bool overUi)> _touches = new Dictionary<int, (Vector2, bool)>();
        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();
        private Vector2 _mouseStart;
        private bool _mouseOverUi;

        private void Update()
        {
            if (Input.touchCount > 0)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                        _touches[touch.fingerId] = (touch.position, IsOverUi(touch.position));
                    else if ((touch.phase == TouchPhase.Ended) && _touches.TryGetValue(touch.fingerId, out var info))
                    {
                        _touches.Remove(touch.fingerId);
                        if (!info.overUi && !IsOverUi(touch.position) && (touch.position - info.start).magnitude < TapMaxMove)
                            HandleTap(touch.position);
                    }
                    else if (touch.phase == TouchPhase.Canceled) _touches.Remove(touch.fingerId);
                }
                return;
            }

            // 모바일에서는 터치를 마우스로도 흉내 내므로, 마우스 탭은 에디터·PC 에서만 처리한다(중복 탭 방지).
            if (UnityEngine.Application.isMobilePlatform) return;
            if (Input.GetMouseButtonDown(0))
            {
                _mouseStart = Input.mousePosition;
                _mouseOverUi = IsOverUi(Input.mousePosition);
            }
            if (Input.GetMouseButtonUp(0) && !_mouseOverUi && ((Vector2)Input.mousePosition - _mouseStart).magnitude < TapMaxMove)
                HandleTap(Input.mousePosition);
        }

        /// <summary>
        /// 그 위치에 UI 가 있는지 직접 레이캐스트한다. IsPointerOverGameObject 는 EventSystem 이 이번 프레임 입력을
        /// 처리했는지(실행 순서)에 따라 결과가 달라져, 창 뒤의 월드까지 탭되는 일이 생긴다.
        /// </summary>
        private static bool IsOverUi(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            UiHits.Clear();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = screenPosition }, UiHits);
            return UiHits.Count > 0;
        }

        public static void HandleTap(Vector2 screenPosition)
        {
            var camera = Camera.main;
            var player = WorldServices.Player;
            if (camera == null || player == null) return;
            var hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPosition), 200f, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var item = hit.collider.GetComponentInParent<GroundItemView>();
                if (item != null)
                {
                    player.WalkToPickup(item);
                    return;
                }
                var npc = hit.collider.GetComponentInParent<NpcController>();
                if (npc != null && npc.IsInteractable)
                {
                    player.WalkToNpc(npc);
                    return;
                }
                var actor = hit.collider.GetComponentInParent<Actor>();
                if (actor != null && actor != player.Actor && !actor.IsDead)
                {
                    player.SetTarget(actor);
                    return;
                }
            }
        }
    }
}
