// 화면 위쪽에 잠깐 떴다 사라지는 안내 문구(토스트)와 확인 대화상자
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class Toast : MonoBehaviour
    {
        private const int MaxVisible = 3;
        private static Toast _instance;
        private RectTransform _stack;
        private readonly List<GameObject> _visible = new List<GameObject>();

        /// <summary>마지막으로 띄운 문구(테스트·QA 자동화에서 확인용).</summary>
        public static string LastMessage { get; private set; }

        public static void Show(string message, float seconds = 2.5f)
        {
            LastMessage = message;
            Debug.Log("[Toast] " + message);
            EnsureInstance().Push(message, seconds);
        }

        private static Toast EnsureInstance()
        {
            if (_instance != null) return _instance;
            var canvas = Ui.CreateCanvas("ToastCanvas", 900);
            // 토스트는 터치를 받지 않는다(아래 버튼 탭을 삼키지 않게).
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            DontDestroyOnLoad(canvas.gameObject);
            _instance = canvas.gameObject.AddComponent<Toast>();
            _instance._stack = Ui.Place(Ui.Node(canvas.transform, "ToastStack"), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -150), new Vector2(1100, 400));
            var layout = _instance._stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            return _instance;
        }

        private void Push(string message, float seconds)
        {
            if (_visible.Count >= MaxVisible)
            {
                Destroy(_visible[0]);
                _visible.RemoveAt(0);
            }
            var row = Ui.Row(_stack, "ToastMessage", 70, new Color(0, 0, 0, 0.78f));
            row.raycastTarget = false;
            var text = Ui.Label(row.transform, "Text", message, 30, TextAnchor.MiddleCenter);
            Ui.Stretch(Ui.Rect(text), 20, 20, 4, 4);
            _visible.Add(row.gameObject);
            StartCoroutine(Expire(row.gameObject, seconds));
        }

        private IEnumerator Expire(GameObject row, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            _visible.Remove(row);
            if (row != null) Destroy(row);
        }
    }

    public static class ConfirmDialog
    {
        public static void Show(Transform parent, string message, string confirmLabel, Action onConfirm)
        {
            var window = Ui.Modal(parent, "ConfirmDialog", new Vector2(900, 380), out var backdrop);
            var text = Ui.Label(window, "Message", message, 32, TextAnchor.MiddleCenter);
            Ui.Place(Ui.Rect(text), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(820, 200));
            var ok = Ui.Button(window, "ConfirmButton", confirmLabel, () =>
            {
                UnityEngine.Object.Destroy(backdrop.gameObject);
                onConfirm();
            }, UiColors.ButtonDanger);
            Ui.Place(Ui.Rect(ok), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 30), new Vector2(260, 90));
            var cancel = Ui.Button(window, "CancelButton", "취소", () => UnityEngine.Object.Destroy(backdrop.gameObject));
            Ui.Place(Ui.Rect(cancel), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(15, 30), new Vector2(260, 90));
        }
    }
}
