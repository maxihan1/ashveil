// 회원가입·로그인·토큰 갱신(리프레시 토큰 회전) API
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ashveil.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/signup", async (SignupRequest req, GameDbContext db, TimeProvider clock) =>
        {
            if (!NameRules.IsValidUsername(req.Username))
                throw GameErrors.Validation("아이디는 4~16자의 영문 소문자, 숫자, 밑줄만 쓸 수 있습니다.");
            if (!NameRules.IsValidPassword(req.Password))
                throw GameErrors.Validation("비밀번호는 6~64자여야 합니다.");

            var account = new Account
            {
                Username = req.Username,
                PasswordHash = PasswordHasher.Hash(req.Password),
                CreatedAt = clock.GetUtcNow().UtcDateTime,
            };
            db.Accounts.Add(account);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                throw GameErrors.Conflict(ErrorCodes.UsernameTaken);
            }
            return Results.Created($"/accounts/{account.Id}", new SignupResponse { AccountId = account.Id });
        });

        group.MapPost("/login", async (LoginRequest req, GameDbContext db, TokenService tokens, JwtOptions jwt, TimeProvider clock) =>
        {
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Username == req.Username);
            if (account == null || req.Password == null || !PasswordHasher.Verify(req.Password, account.PasswordHash))
                throw GameErrors.Unauthorized(ErrorCodes.InvalidCredentials);
            return await IssueAsync(db, tokens, jwt, account, clock.GetUtcNow().UtcDateTime);
        });

        group.MapPost("/refresh", async (RefreshRequest req, GameDbContext db, TokenService tokens, JwtOptions jwt, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var hash = TokenService.HashRefreshToken(req.RefreshToken ?? "");
            var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash)
                         ?? throw GameErrors.Unauthorized(ErrorCodes.InvalidRefreshToken);

            if (stored.RevokedAt != null)
            {
                // 이미 쓴 토큰이 다시 오면 탈취로 보고 그 계정의 모든 토큰을 폐기한다.
                await db.RefreshTokens.Where(t => t.AccountId == stored.AccountId && t.RevokedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
                throw GameErrors.Unauthorized(ErrorCodes.InvalidRefreshToken);
            }
            if (stored.ExpiresAt <= now) throw GameErrors.Unauthorized(ErrorCodes.InvalidRefreshToken);

            // 동시에 같은 토큰으로 갱신하면 한쪽만 성공한다.
            var revoked = await db.RefreshTokens.Where(t => t.Id == stored.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now));
            if (revoked == 0) throw GameErrors.Unauthorized(ErrorCodes.InvalidRefreshToken);

            var account = await db.Accounts.SingleAsync(a => a.Id == stored.AccountId);
            return await IssueAsync(db, tokens, jwt, account, now);
        });
    }

    private static async Task<TokenResponse> IssueAsync(GameDbContext db, TokenService tokens, JwtOptions jwt, Account account, DateTime now)
    {
        var (access, expiresIn) = tokens.CreateAccessToken(account.Id, account.Username);
        var refresh = TokenService.NewRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            AccountId = account.Id,
            TokenHash = TokenService.HashRefreshToken(refresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(jwt.RefreshTokenDays),
        });
        await db.SaveChangesAsync();
        return new TokenResponse { AccessToken = access, RefreshToken = refresh, ExpiresInSeconds = expiresIn };
    }
}
