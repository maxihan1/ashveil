// 「강화하기」 버튼 잠금: 강화 응답을 기다리는 동안과 결과가 뜬 뒤 0.5초 동안은 눌러도 요청을 보내지 않는다(기획서 §7)
using System;
using System.Threading.Tasks;

namespace Ashveil.Domain
{
    public sealed class EnhanceButtonLock
    {
        public const int HoldMilliseconds = 500;

        public bool IsLocked { get; private set; }

        /// <summary>
        /// 잠겨 있으면 아무것도 하지 않고 false. 아니면 잠근 채 requestAndShow(요청 → 결과 표시)를 돌리고
        /// 결과 뒤 HoldMilliseconds 가 지나면 푼다. 요청이 실패하면 보여 줄 결과가 없으니 바로 풀고 예외를 그대로 넘긴다.
        /// </summary>
        public async Task<bool> RunAsync(Func<Task> requestAndShow)
        {
            if (IsLocked) return false;
            IsLocked = true;
            try
            {
                await requestAndShow();
                await Task.Delay(HoldMilliseconds);
            }
            finally
            {
                IsLocked = false;
            }
            return true;
        }
    }
}
