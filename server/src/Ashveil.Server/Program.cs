// 서버 진입점: DI 구성, JWT 인증, OpenAPI, 마이그레이션·봇·스폰 시드, 엔드포인트 등록
using System.Text.Json.Serialization;
using Ashveil.Server.Data;
using Ashveil.Server.Endpoints;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 설정은 서비스가 처음 쓰일 때 읽는다(테스트 호스트가 덮어쓴 값도 반영되도록).
builder.Services.AddSingleton(sp =>
{
    var jwt = sp.GetRequiredService<IConfiguration>().GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
    if (jwt.Key.Length < 32) throw new InvalidOperationException("Jwt:Key 는 32자 이상이어야 합니다. 환경 변수 Jwt__Key 로 지정하세요.");
    return jwt;
});
builder.Services.AddSingleton(sp =>
{
    var config = new GameConfig();
    sp.GetRequiredService<IConfiguration>().GetSection("Game").Bind(config);
    return new RuntimeGameConfig(config);
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IRandomSource, SharedRandomSource>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<ForcedOutcomes>();
builder.Services.AddScoped<BotService>();

builder.Services.AddDbContext<GameDbContext>((sp, o) => o
    .UseNpgsql(sp.GetRequiredService<IConfiguration>().GetConnectionString("Game"))
    .UseSnakeCaseNamingConvention());

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// 모델 바인딩 실패(필드 타입 오류 등)를 Development 밖에서도 예외로 올려 400 VALIDATION_FAILED 본문을 준다. 끄면 빈 400 이다.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtOptions>((o, jwt) =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.SigningKey(),
        ClockSkew = TimeSpan.FromSeconds(30),
    };
    o.Events = new JwtBearerEvents
    {
        // 인증 실패도 { code, message } 형식으로 돌려준다.
        OnChallenge = async ctx =>
        {
            ctx.HandleResponse();
            ctx.Response.StatusCode = 401;
            await ctx.Response.WriteAsJsonAsync(new Ashveil.Shared.Dto.ErrorResponse
            {
                Code = ErrorCodes.Unauthorized, Message = GameErrors.DefaultMessage(ErrorCodes.Unauthorized),
            });
        },
    };
});
builder.Services.AddAuthorization();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer((doc, _, _) =>
{
    doc.Info.Title = "Ashveil Game API";
    doc.Info.Description = "게임 API에는 Authorization: Bearer <token>, 캐릭터 대상 API에는 X-Character-Id 헤더가 필요합니다.";
    return Task.CompletedTask;
}));

var app = builder.Build();

app.UseGameErrorHandler();
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Ashveil Game API");
        o.RoutePrefix = "swagger";
    });
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", environment = app.Environment.EnvironmentName })).WithTags("Health");
app.MapAuthEndpoints();
app.MapCharacterEndpoints();
app.MapShopEndpoints();
app.MapCombatEndpoints();
app.MapWorldEndpoints();
app.MapGmEndpoints();
app.MapAdminEndpoints();
app.MapBackoffice();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<JwtOptions>(); // 서명 키가 없으면 시작할 때 바로 실패한다
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<BotService>().SeedIfEmptyAsync();
    await SpawnService.SeedIfEmptyAsync(db);
}

app.Run();

public partial class Program;
