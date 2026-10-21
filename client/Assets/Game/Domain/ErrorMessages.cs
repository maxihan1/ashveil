// 서버 에러 code → 플레이어에게 보여 줄 한국어 안내 문구 매핑
using System.Collections.Generic;
using Ashveil.Shared;

namespace Ashveil.Domain
{
    public static class ErrorMessages
    {
        private static readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            [ErrorCodes.ValidationFailed] = "입력값을 확인해 주세요.",
            [ErrorCodes.Unauthorized] = "로그인이 필요합니다.",
            [ErrorCodes.InvalidCredentials] = "아이디 또는 비밀번호가 올바르지 않습니다.",
            [ErrorCodes.InvalidRefreshToken] = "로그인 정보가 만료되었습니다. 다시 로그인해 주세요.",
            [ErrorCodes.Forbidden] = "접근 권한이 없습니다.",
            [ErrorCodes.NotFound] = "대상을 찾을 수 없습니다.",
            [ErrorCodes.UsernameTaken] = "이미 사용 중인 아이디입니다.",
            [ErrorCodes.NameTaken] = "이미 사용 중인 캐릭터 이름입니다.",
            [ErrorCodes.CharacterLimit] = "캐릭터는 최대 3개까지 만들 수 있습니다.",
            [ErrorCodes.InsufficientGold] = "골드가 부족합니다.",
            [ErrorCodes.InventoryFull] = "인벤토리가 가득 찼습니다.",
            [ErrorCodes.NotEnoughQuantity] = "수량이 부족합니다.",
            [ErrorCodes.ItemEquipped] = "장착 중인 아이템은 판매할 수 없습니다.",
            [ErrorCodes.NotSellable] = "판매할 수 없는 아이템입니다.",
            [ErrorCodes.NotEquippable] = "장착하거나 강화할 수 없는 아이템입니다.",
            [ErrorCodes.NotUsable] = "사용할 수 없는 아이템입니다.",
            [ErrorCodes.ScrollMismatch] = "이 장비에는 사용할 수 없는 주문서입니다.",
            [ErrorCodes.MaxEnhance] = "더 이상 강화할 수 없습니다.",
            [ErrorCodes.InvalidSpawn] = "알 수 없는 몬스터입니다.",
            [ErrorCodes.OutOfRange] = "대상과 너무 멀리 떨어져 있습니다.",
            [ErrorCodes.Cooldown] = "잠시 후 다시 시도해 주세요.",
            [ErrorCodes.SafeZone] = "안전지대에서는 전투할 수 없습니다.",
            [ErrorCodes.BotDead] = "이미 쓰러진 대상입니다.",
            [ErrorCodes.InvalidTarget] = "공격할 수 없는 대상입니다.",
            [ErrorCodes.ItemExpired] = "아이템이 사라졌습니다.",
            [ErrorCodes.GmDisabled] = "GM 기능은 QA 서버에서만 쓸 수 있습니다.",
            [ErrorCodes.AdminDisabled] = "어드민 기능은 QA 서버에서만 쓸 수 있습니다.",
            [ErrorCodes.Internal] = "서버 오류가 발생했습니다. 잠시 후 다시 시도해 주세요.",
            [ErrorCodes.NetworkError] = "서버에 연결할 수 없습니다. 네트워크와 서버 주소를 확인해 주세요.",
        };

        public static string For(string code) =>
            code != null && Messages.TryGetValue(code, out var message) ? message : "요청을 처리하지 못했습니다.";

        public static bool IsKnown(string code) => code != null && Messages.ContainsKey(code);
    }

    public static class AlignmentView
    {
        /// <summary>이름 색(16진 RGB). 선량 파랑, 선공 보라, 악명 빨강.</summary>
        public static string ColorHex(AlignmentState state)
        {
            switch (state)
            {
                case AlignmentState.Purple: return "#B266FF";
                case AlignmentState.Chaotic: return "#FF4040";
                default: return "#5AA9FF";
            }
        }

        public static string Label(AlignmentState state)
        {
            switch (state)
            {
                case AlignmentState.Purple: return "선공";
                case AlignmentState.Chaotic: return "악명";
                default: return "선량";
            }
        }

        public static string ClassLabel(ClassType classType)
        {
            switch (classType)
            {
                case ClassType.Archer: return "궁수";
                case ClassType.Mage: return "마법사";
                default: return "전사";
            }
        }

        public static string TemperamentLabel(Temperament temperament)
        {
            switch (temperament)
            {
                case Temperament.Aggressive: return "선공형";
                case Temperament.Retaliate: return "반격형";
                default: return "평화형";
            }
        }
    }
}
