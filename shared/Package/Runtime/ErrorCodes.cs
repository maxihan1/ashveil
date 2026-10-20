// 서버 에러 응답 code 값 상수 (클라이언트 안내 문구 매핑 키)
namespace Ashveil.Shared
{
    public static class ErrorCodes
    {
        public const string ValidationFailed = "VALIDATION_FAILED";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string InvalidCredentials = "INVALID_CREDENTIALS";
        public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
        public const string Forbidden = "FORBIDDEN";
        public const string NotFound = "NOT_FOUND";

        public const string UsernameTaken = "USERNAME_TAKEN";
        public const string NameTaken = "NAME_TAKEN";
        public const string CharacterLimit = "CHARACTER_LIMIT";

        public const string InsufficientGold = "INSUFFICIENT_GOLD";
        public const string InventoryFull = "INVENTORY_FULL";
        public const string NotEnoughQuantity = "NOT_ENOUGH_QUANTITY";
        public const string ItemEquipped = "ITEM_EQUIPPED";
        public const string NotSellable = "NOT_SELLABLE";

        public const string NotEquippable = "NOT_EQUIPPABLE";
        public const string NotUsable = "NOT_USABLE";
        public const string ScrollMismatch = "SCROLL_MISMATCH";
        public const string MaxEnhance = "MAX_ENHANCE";

        public const string InvalidSpawn = "INVALID_SPAWN";
        public const string OutOfRange = "OUT_OF_RANGE";
        public const string Cooldown = "COOLDOWN";
        public const string SafeZone = "SAFE_ZONE";
        public const string BotDead = "BOT_DEAD";
        public const string InvalidTarget = "INVALID_TARGET";
        public const string ItemExpired = "ITEM_EXPIRED";

        public const string GmDisabled = "GM_DISABLED";
        public const string Internal = "INTERNAL_ERROR";

        /// <summary>클라이언트 전용. 서버에 연결하지 못했을 때.</summary>
        public const string NetworkError = "NETWORK_ERROR";
    }
}
