// 런타임 uGUI 생성 헬퍼 (캔버스·패널·텍스트·버튼·입력창·스크롤 목록·막대)
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public static class UiColors
    {
        public static readonly Color Panel = new Color(0.08f, 0.09f, 0.12f, 0.92f);
        public static readonly Color PanelLight = new Color(0.16f, 0.17f, 0.22f, 0.95f);
        public static readonly Color Button = new Color(0.24f, 0.30f, 0.42f, 1f);
        public static readonly Color ButtonAccent = new Color(0.78f, 0.55f, 0.18f, 1f);
        public static readonly Color ButtonDanger = new Color(0.62f, 0.18f, 0.18f, 1f);
        public static readonly Color Text = new Color(0.94f, 0.94f, 0.96f, 1f);
        public static readonly Color TextDim = new Color(0.65f, 0.67f, 0.72f, 1f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.3f, 1f);
        public static readonly Color Hp = new Color(0.85f, 0.2f, 0.2f, 1f);
        public static readonly Color Mp = new Color(0.25f, 0.45f, 0.95f, 1f);
        public static readonly Color Exp = new Color(0.95f, 0.8f, 0.2f, 1f);
        public static readonly Color Good = new Color(0.35f, 0.85f, 0.45f, 1f);
        public static readonly Color Bad = new Color(0.95f, 0.35f, 0.35f, 1f);

        public static Color FromHex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
    }

    public static class Ui
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        private static Font _font;
        private static Sprite _circle;

        public static Font Font
        {
            get
            {
                if (_font != null) return _font;
                _font = Resources.Load<Font>("Fonts/NanumGothic-Regular");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>원형 버튼·조이스틱·미니맵용 스프라이트(런타임 생성).</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                var r = size / 2f;
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Sqrt((x - r + 0.5f) * (x - r + 0.5f) + (y - r + 0.5f) * (y - r + 0.5f));
                    var a = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
                tex.SetPixels32(pixels);
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
                return _circle;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static RectTransform Rect(Component c) => (RectTransform)c.transform;

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>앵커 한 점 기준 배치. anchor·pivot 은 (0~1).</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Node(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int size = 30, TextAnchor align = TextAnchor.MiddleLeft, Color? color = null)
        {
            var rt = Node(parent, name);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.alignment = align;
            label.color = color ?? UiColors.Text;
            label.text = text;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            return label;
        }

        public static Button Button(Transform parent, string name, string text, Action onClick, Color? color = null, int fontSize = 30)
        {
            var image = Panel(parent, name, color ?? UiColors.Button);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = Label(image.transform, "Label", text, fontSize, TextAnchor.MiddleCenter);
            Stretch(Rect(label), 6, 6, 4, 4);
            return button;
        }

        public static Text ButtonLabel(Button button) => button.transform.Find("Label").GetComponent<Text>();

        public static InputField Input(Transform parent, string name, string placeholder, bool password = false, int fontSize = 30)
        {
            var image = Panel(parent, name, new Color(0.03f, 0.03f, 0.05f, 0.95f));
            var field = image.gameObject.AddComponent<InputField>();
            var text = Label(image.transform, "Text", "", fontSize);
            text.supportRichText = false;
            Stretch(Rect(text), 16, 16, 6, 6);
            var hint = Label(image.transform, "Placeholder", placeholder, fontSize, TextAnchor.MiddleLeft, UiColors.TextDim);
            hint.fontStyle = FontStyle.Italic;
            Stretch(Rect(hint), 16, 16, 6, 6);
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            if (password) field.contentType = InputField.ContentType.Password;
            return field;
        }

        /// <summary>세로 스크롤 목록. 반환값의 content 아래에 행을 붙인다.</summary>
        public static RectTransform ScrollList(Transform parent, string name, out ScrollRect scroll, float spacing = 8)
        {
            var root = Panel(parent, name, new Color(0, 0, 0, 0.25f));
            root.gameObject.AddComponent<RectMask2D>();
            scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            var content = Node(root.transform, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = Rect(root);
            scroll.content = content;
            return content;
        }

        /// <summary>목록 한 행. LayoutElement 높이를 고정한다.</summary>
        public static Image Row(Transform content, string name, float height, Color? color = null)
        {
            var row = Panel(content, name, color ?? UiColors.PanelLight);
            var element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return row;
        }

        public static void Clear(Transform content)
        {
            for (var i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject);
        }

        public static Bar Bar(Transform parent, string name, Color fill, Color? background = null)
        {
            var bg = Panel(parent, name, background ?? new Color(0, 0, 0, 0.6f));
            var fillImage = Panel(bg.transform, "Fill", fill);
            var fillRt = Rect(fillImage);
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0, 0.5f);
            var text = Label(bg.transform, "Text", "", 22, TextAnchor.MiddleCenter);
            Stretch(Rect(text));
            return new Bar(fillRt, text);
        }

        /// <summary>화면 전체를 덮는 반투명 배경 + 가운데 창.</summary>
        public static RectTransform Modal(Transform parent, string name, Vector2 size, out Image backdrop)
        {
            backdrop = Panel(parent, name, new Color(0, 0, 0, 0.55f));
            Stretch(Rect(backdrop));
            var window = Panel(backdrop.transform, "Window", UiColors.Panel);
            return Place(Rect(window), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }
    }

    public sealed class Bar
    {
        private readonly RectTransform _fill;
        public Text Text { get; }

        public Bar(RectTransform fill, Text text)
        {
            _fill = fill;
            Text = text;
        }

        public RectTransform Root => (RectTransform)_fill.parent;

        public void Set(float ratio, string label = null)
        {
            _fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1);
            if (label != null) Text.text = label;
        }
    }
}
