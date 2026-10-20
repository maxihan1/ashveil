// 도메인 에러 예외와 { code, message } 통일 응답을 만드는 예외 처리기
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.AspNetCore.Diagnostics;
using Npgsql;

namespace Ashveil.Server.Infrastructure;

public sealed class GameException(string code, int status, string? message = null)
    : Exception(message ?? GameErrors.DefaultMessage(code))
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}

public static class GameErrors
{
    public static GameException Validation(string message) => new(ErrorCodes.ValidationFailed, 400, message);
    public static GameException NotFound(string what) => new(ErrorCodes.NotFound, 404, $"{what}을(를) 찾을 수 없습니다.");
    public static GameException Conflict(string code) => new(code, 409);
    public static GameException Unauthorized(string code = ErrorCodes.Unauthorized) => new(code, 401);
    public static GameException Forbidden() => new(ErrorCodes.Forbidden, 403);

    public static string DefaultMessage(string code) => code switch
    {
        ErrorCodes.Unauthorized => "로그인이 필요합니다.",
        ErrorCodes.InvalidCredentials => "아이디 또는 비밀번호가 올바르지 않습니다.",
        ErrorCodes.InvalidRefreshToken => "로그인 정보가 만료되었습니다. 다시 로그인해 주세요.",
        ErrorCodes.Forbidden => "접근 권한이 없습니다.",
        ErrorCodes.UsernameTaken => "이미 사용 중인 아이디입니다.",
        ErrorCodes.NameTaken => "이미 사용 중인 캐릭터 이름입니다.",
        ErrorCodes.CharacterLimit => "캐릭터는 계정당 최대 3개까지 만들 수 있습니다.",
        ErrorCodes.InsufficientGold => "골드가 부족합니다.",
        ErrorCodes.InventoryFull => "인벤토리가 가득 찼습니다.",
        ErrorCodes.NotEnoughQuantity => "수량이 부족합니다.",
        ErrorCodes.ItemEquipped => "장착 중인 아이템입니다. 먼저 해제해 주세요.",
        ErrorCodes.NotSellable => "판매할 수 없는 아이템입니다.",
        ErrorCodes.NotEquippable => "장착하거나 강화할 수 없는 아이템입니다.",
        ErrorCodes.ScrollMismatch => "이 장비에 사용할 수 없는 주문서입니다.",
        ErrorCodes.MaxEnhance => "더 이상 강화할 수 없습니다.",
        ErrorCodes.InvalidSpawn => "알 수 없는 몬스터입니다.",
        ErrorCodes.OutOfRange => "대상과 너무 멀리 떨어져 있습니다.",
        ErrorCodes.Cooldown => "아직 처리할 수 없습니다. 잠시 후 다시 시도해 주세요.",
        ErrorCodes.SafeZone => "안전지대에서는 전투할 수 없습니다.",
        ErrorCodes.BotDead => "이미 쓰러진 대상입니다.",
        ErrorCodes.InvalidTarget => "올바르지 않은 대상입니다.",
        ErrorCodes.ItemExpired => "아이템이 사라졌습니다.",
        ErrorCodes.GmDisabled => "GM 기능은 QA 환경에서만 사용할 수 있습니다.",
        ErrorCodes.NotFound => "대상을 찾을 수 없습니다.",
        _ => "요청을 처리할 수 없습니다.",
    };

    public static IResult ToResult(string code, int status, string? message = null) =>
        Results.Json(new ErrorResponse { Code = code, Message = message ?? DefaultMessage(code) }, statusCode: status);

    /// <summary>모든 예외를 ErrorResponse 로 바꾼다. 알 수 없는 예외는 500 INTERNAL_ERROR.</summary>
    public static void UseGameErrorHandler(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(async http =>
        {
            var error = http.Features.Get<IExceptionHandlerFeature>()?.Error;
            var (code, status, message) = error switch
            {
                GameException g => (g.Code, g.Status, g.Message),
                BadHttpRequestException b => (ErrorCodes.ValidationFailed, 400, "요청 형식이 올바르지 않습니다. " + b.Message),
                Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } }
                    => (ErrorCodes.InsufficientGold, 409, DefaultMessage(ErrorCodes.InsufficientGold)),
                _ => (ErrorCodes.Internal, 500, "서버 오류가 발생했습니다."),
            };
            if (status >= 500)
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Ashveil.Errors")
                    .LogError(error, "Unhandled error on {Path}", http.Request.Path);
            http.Response.StatusCode = status;
            await http.Response.WriteAsJsonAsync(new ErrorResponse { Code = code, Message = message });
        }));
    }
}
