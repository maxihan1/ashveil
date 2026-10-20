// 상점 가격(악명 할증)과 계정·캐릭터 이름 검증 규칙
using System.Text.RegularExpressions;

namespace Ashveil.Shared
{
    public static class ShopRules
    {
        public static int PricePercent(ShopConfig config, AlignmentState state) =>
            state == AlignmentState.Chaotic ? config.ChaoticPricePercent : 100;

        /// <summary>총액 = 구매가 × 수량 × 배율, 올림.</summary>
        public static long BuyPrice(ShopConfig config, ItemTemplate template, int quantity, AlignmentState state)
        {
            var total = template.BuyPrice * quantity;
            return (total * PricePercent(config, state) + 99) / 100;
        }
    }

    public static class NameRules
    {
        public const int MaxCharactersPerAccount = 3;
        public const int PasswordMinLength = 6;
        public const int PasswordMaxLength = 64;

        private static readonly Regex CharacterName = new Regex("^[가-힣A-Za-z0-9]{2,12}$");
        private static readonly Regex Username = new Regex("^[a-z0-9_]{4,16}$");

        public static bool IsValidCharacterName(string name) => name != null && CharacterName.IsMatch(name);

        public static bool IsValidUsername(string username) => username != null && Username.IsMatch(username);

        public static bool IsValidPassword(string password) =>
            password != null && password.Length >= PasswordMinLength && password.Length <= PasswordMaxLength;
    }
}
