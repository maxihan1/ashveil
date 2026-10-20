// UI 점(dot) 미니맵: 플레이어 중심 반경 60m 안의 몬스터·봇·NPC·바닥 아이템을 색 점으로 표시
using System.Collections.Generic;
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class MinimapView : MonoBehaviour
    {
        private const float RangeMeters = 60f;
        private const float SizePixels = 280f;
        private const float RefreshSeconds = 0.2f;

        private RectTransform _dotLayer;
        private Text _coordinates;
        private readonly List<Image> _pool = new List<Image>();
        private float _timer;

        public static MinimapView Create(Transform parent)
        {
            var frame = Ui.Panel(parent, "Minimap", new Color(0.05f, 0.12f, 0.08f, 0.75f));
            frame.sprite = Ui.Circle;
            Ui.Place(frame.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(SizePixels, SizePixels));
            var view = frame.gameObject.AddComponent<MinimapView>();
            view._dotLayer = Ui.Stretch(Ui.Node(frame.transform, "Dots"));
            var me = Ui.Panel(frame.transform, "PlayerDot", new Color(0.3f, 1f, 0.4f));
            me.sprite = Ui.Circle;
            Ui.Place(me.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 18));
            view._coordinates = Ui.Label(frame.transform, "CoordinatesText", "", 20, TextAnchor.MiddleCenter, UiColors.TextDim);
            Ui.Place(view._coordinates.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(260, 30));
            return view;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = RefreshSeconds;
            var player = WorldServices.Player;
            if (player == null) return;

            var center = player.Actor.Flat;
            _coordinates.text = $"({center.X:0}, {center.Z:0})";
            var used = 0;
            var scale = SizePixels * 0.5f / RangeMeters;
            foreach (var actor in Actor.All)
            {
                if (actor == player.Actor || actor.IsDead) continue;
                var offset = actor.Flat - center;
                if (offset.Length > RangeMeters) continue;
                var dot = Dot(used++);
                dot.color = ColorOf(actor);
                dot.rectTransform.anchoredPosition = new Vector2(offset.X * scale, offset.Z * scale);
            }
            for (var i = used; i < _pool.Count; i++)
                if (_pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
        }

        private Image Dot(int index)
        {
            if (index >= _pool.Count)
            {
                var image = Ui.Panel(_dotLayer, "Dot", Color.white);
                image.sprite = Ui.Circle;
                image.raycastTarget = false;
                Ui.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 12));
                _pool.Add(image);
            }
            var dot = _pool[index];
            if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            return dot;
        }

        private static Color ColorOf(Actor actor)
        {
            switch (actor.Team)
            {
                case Team.Monster: return new Color(1f, 0.3f, 0.25f);
                case Team.Bot: return UiColors.FromHex(AlignmentView.ColorHex(actor.Alignment));
                case Team.Guard:
                case Team.Npc: return actor.GetComponent<GroundItemView>() != null ? Color.white : new Color(1f, 0.9f, 0.3f);
                default: return Color.white;
            }
        }
    }
}
