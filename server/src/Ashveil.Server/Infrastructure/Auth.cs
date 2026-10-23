// JWT 발급, 비밀번호 해시(PBKDF2), 리프레시 토큰 해시, 요청 컨텍스트(계정·캐릭터) 추출
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ashveil.Server.Infrastructure;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "ashveil";
    public string Audience { get; set; } = "ashveil-client";
    /// <summary>HS256 서명 키. 32바이트 이상. 운영에서는 환경 변수 Jwt__Key 로 넣는다.</summary>
    public string Key { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public sealed class TokenService(JwtOptions options, TimeProvider clock)
{
    public (string Token, int ExpiresInSeconds) CreateAccessToken(long accountId, string username)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()), new Claim("username", username)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(options.AccessTokenMinutes),
            SigningCredentials = new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256),
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), options.AccessTokenMinutes * 60);
    }

    public static string NewRefreshToken() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class PasswordHasher
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"v1.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != "v1") return false;
        var iterations = int.Parse(parts[1]);
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public static class RequestContext
{
    public const string CharacterHeader = "X-Character-Id";

    public static long AccountId(this HttpContext http)
    {
        var sub = http.User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(sub, out var id) ? id : throw GameErrors.Unauthorized();
    }

    /// <summary>X-Character-Id 헤더 값. 소유권은 잠금 조회에서 함께 검사한다.</summary>
    public static long CharacterId(this HttpContext http)
    {
        var raw = http.Request.Headers[CharacterHeader].ToString();
        return long.TryParse(raw, out var id) && id > 0
            ? id
            : throw GameErrors.Validation($"{CharacterHeader} 헤더가 필요합니다.");
    }
}
