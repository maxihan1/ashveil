// 머리 위 이름표(이름·레벨·성향 색·HP 막대)와 떠오르는 데미지 숫자를 한 캔버스에서 그린다
using System.Collections.Generic;
using Ashveil.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class NameplateLayer : MonoBehaviour
    {
        private const float VisibleDistance = 45f;
        private static NameplateLayer _instance;

        private sealed class Plate
        {
            public Actor Actor;
            public RectTransform Root;
            public Text Name;
            public Bar Hp;
            public string LastLabel;
        }

        private sealed class PopupEntry
        {
            public RectTransform Root;
            public Text Text;
            public Vector3 World;
            public float Age;
        }

        private readonly Dictionary<Actor, Plate> _plates = new Dictionary<Actor, Plate>();
        private readonly List<PopupEntry> _popups = new List<PopupEntry>();
        private readonly List<Actor> _stale = new List<Actor>();
        private RectTransform _root;
        private Camera _camera;

        /// <summary>대상 표시용으로 HP 막대를 항상 보여 줄 개체.</summary>
        public static Actor Highlighted { get; set; }

        public static void Create(Transform parent)
        {
            var canvas = Ui.CreateCanvas("NameplateCanvas", 5, parent);
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            _instance = canvas.gameObject.AddComponent<NameplateLayer>();
            _instance._root = Ui.Rect(canvas);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void Popup(Actor actor, string text, Color color)
        {
            if (_instance == null || actor == null) return;
            var label = Ui.Label(_instance._root, "Popup", text, 34, TextAnchor.MiddleCenter, color);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.sizeDelta = new Vector2(300, 60);
            _instance._popups.Add(new PopupEntry { Root = label.rectTransform, Text = label, World = actor.HeadPosition });
        }

        public static void PopupAt(Vector3 world, string text, Color color)
        {
            if (_instance == null) return;
            var label = Ui.Label(_instance._root, "Popup", text, 32, TextAnchor.MiddleCenter, color);
            label.rectTransform.sizeDelta = new Vector2(500, 60);
            _instance._popups.Add(new PopupEntry { Root = label.rectTransform, Text = label, World = world });
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            foreach (var actor in Actor.All)
                if (!_plates.ContainsKey(actor)) _plates[actor] = CreatePlate(actor);

            _stale.Clear();
            foreach (var pair in _plates)
            {
                var plate = pair.Value;
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                {
                    _stale.Add(pair.Key);
                    continue;
                }
                UpdatePlate(plate);
            }
            foreach (var actor in _stale)
            {
                if (_plates[actor].Root != null) Destroy(_plates[actor].Root.gameObject);
                _plates.Remove(actor);
            }

            for (var i = _popups.Count - 1; i >= 0; i--)
            {
                var popup = _popups[i];
                popup.Age += Time.deltaTime;
                var screen = _camera.WorldToScreenPoint(popup.World);
                popup.Root.position = screen + new Vector3(0, popup.Age * 90f, 0);
                var c = popup.Text.color;
                c.a = 1f - popup.Age;
                popup.Text.color = c;
                if (popup.Age >= 1f || screen.z < 0)
                {
                    Destroy(popup.Root.gameObject);
                    _popups.RemoveAt(i);
                }
            }
        }

        private Plate CreatePlate(Actor actor)
        {
            var root = Ui.Node(_root, "Nameplate_" + actor.name);
            root.sizeDelta = new Vector2(320, 70);
            var name = Ui.Label(root, "Name", "", 26, TextAnchor.MiddleCenter);
            var outline = name.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.9f);
            Ui.Place(name.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(320, 36));
            var hp = Ui.Bar(root, "HpBar", UiColors.Hp);
            hp.Text.text = "";
            Ui.Place(hp.Root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(140, 12));
            return new Plate { Actor = actor, Root = root, Name = name, Hp = hp };
        }

        private void UpdatePlate(Plate plate)
        {
            var actor = plate.Actor;
            var screen = _camera.WorldToScreenPoint(actor.HeadPosition);
            var visible = screen.z > 0 && screen.z < VisibleDistance && !(actor.IsDead && actor.Team != Team.Player);
            if (plate.Root.gameObject.activeSelf != visible) plate.Root.gameObject.SetActive(visible);
            if (!visible) return;
            plate.Root.position = screen;

            var label = LabelOf(actor);
            if (label != plate.LastLabel)
            {
                plate.Name.text = label;
                plate.LastLabel = label;
            }
            plate.Name.color = ColorOf(actor);
            var showHp = actor.Team == Team.Monster || actor.Team == Team.Bot || actor.Team == Team.Player;
            var damaged = actor.Hp < actor.MaxHp || actor == Highlighted;
            plate.Hp.Root.gameObject.SetActive(showHp && damaged);
            if (showHp && damaged) plate.Hp.Set((float)actor.Hp / actor.MaxHp);
        }

        /// <summary>기획서 §9 봇 이름표 꼴 「이름 Lv.N」. 몬스터·플레이어도 같은 꼴로 맞춘다.</summary>
        public static string LabelOf(Actor actor)
        {
            switch (actor.Team)
            {
                case Team.Monster:
                case Team.Bot:
                case Team.Player:
                    return $"{actor.DisplayName} Lv.{actor.Level}";
                default:
                    return actor.DisplayName;
            }
        }

        private static Color ColorOf(Actor actor)
        {
            switch (actor.Team)
            {
                case Team.Monster: return new Color(1f, 0.85f, 0.6f);
                case Team.Guard:
                case Team.Npc: return new Color(0.6f, 1f, 0.6f);
                default: return UiColors.FromHex(AlignmentView.ColorHex(actor.Alignment));
            }
        }
    }
}
