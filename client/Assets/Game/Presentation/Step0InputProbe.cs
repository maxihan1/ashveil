// 0단계 시험: 게임 어셈블리 안에서 Input.touchCount 가 가짜 터치를 보는지 logcat 으로 찍는다
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class Step0InputProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot() => DontDestroyOnLoad(new GameObject("[Step0InputProbe]", typeof(Step0InputProbe)));

        private void Update()
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                Debug.Log($"[Step0] {typeof(Input).AssemblyQualifiedName} touch finger={t.fingerId} phase={t.phase} pos={t.position}");
            }
        }
    }
}
