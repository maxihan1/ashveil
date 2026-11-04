// Boss Room 애니메이션 클립의 OnAnimEvent 이벤트를 받아 "수신자 없음" 오류를 막는 빈 수신기 (프리팹에 저장되므로 파일명=클래스명)
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class AnimEventReceiver : MonoBehaviour
    {
        public void OnAnimEvent(AnimationEvent animationEvent) { }
    }
}
