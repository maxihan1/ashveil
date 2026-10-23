// 인증·공통 에러 응답 DTO
namespace Ashveil.Shared.Dto
{
    public sealed class SignupRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public sealed class SignupResponse
    {
        public long AccountId { get; set; }
    }

    public sealed class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken { get; set; }
    }

    public sealed class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public int ExpiresInSeconds { get; set; }
    }

    public sealed class ErrorResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }
}
