// 모달 창 공통 틀(제목·닫기 버튼·한 번에 하나만 열기)과 탭 버튼 묶음
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public abstract class PanelBase : MonoBehaviour
    {
        protected RectTransform Window { get; private set; }
        protected RectTransform Body { get; private set; }

        protected static T OpenSingle<T>(Transform layer, string name, string title, Vector2 size) where T : PanelBase
        {
            var existing = layer.GetComponentInChildren<T>();
            if (existing != null)
            {
                existing.Refresh();
                return existing;
            }
            var window = Ui.Modal(layer, name, size, out var backdrop);
            var panel = backdrop.gameObject.AddComponent<T>();
            panel.Window = window;
            var titleText = Ui.Label(window, "Title", title, 40, TextAnchor.MiddleLeft, UiColors.Gold);
            Ui.Place(titleText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -14), new Vector2(size.x - 200, 64));
            var close = Ui.Button(window, "CloseButton", "닫기", panel.Close, UiColors.ButtonDanger, 28);
            Ui.Place(Ui.Rect(close), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(140, 64));
            panel.Body = Ui.Stretch(Ui.Node(window, "Body"), 24, 24, 90, 24);
            panel.Build();
            panel.Refresh();
            return panel;
        }

        protected abstract void Build();

        public virtual void Refresh() { }

        public void Close()
        {
            if (this != null) Destroy(gameObject);
        }
    }

    /// <summary>가로로 늘어선 탭 버튼. 고른 탭은 강조색으로 바뀐다.</summary>
    public sealed class Tabs
    {
        private readonly List<(Button button, Action select)> _tabs = new List<(Button, Action)>();

        public Tabs(Transform parent, string name, Vector2 position, float width)
        {
            Root = Ui.Place(Ui.Node(parent, name), new Vector2(0, 1), new Vector2(0, 1), position, new Vector2(width, 70));
            var layout = Root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
        }

        public RectTransform Root { get; }

        public void Add(string name, string label, Action select)
        {
            var index = _tabs.Count;
            var button = Ui.Button(Root, name, label, () => Select(index), null, 28);
            _tabs.Add((button, select));
        }

        public void Select(int index)
        {
            for (var i = 0; i < _tabs.Count; i++) _tabs[i].button.image.color = i == index ? UiColors.ButtonAccent : UiColors.Button;
            _tabs[index].select();
        }
    }
}
