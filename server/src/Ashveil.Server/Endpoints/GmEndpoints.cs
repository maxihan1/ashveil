// QA 환경 전용 GM API (골드·아이템·레벨·강화 확률·봇 리셋·성향치·드랍 확률)
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Server.Endpoints;

public static class GmEndpoints
{
    public const string QaEnvironment = "Qa";

    public static void MapGmEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsEnvironment(QaEnvironment))
        {
            // QA가 아니면 GM 경로 전체가 GM_DISABLED 를 돌려준다.
            app.Map("/gm/{**rest}", () => GameErrors.ToResult(ErrorCodes.GmDisabled, 404)).ExcludeFromDescription();
            return;
        }

        var gm = app.MapGroup("/gm").WithTags("GM (QA only)").RequireAuthorization();

        gm.MapPost("/gold", async (GmGoldRequest req, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                db.ChangeGold(character, req.Amount, LedgerReasons.GmGrant, "Gm", "gold", now);
            });
            return await db.CharacterDtoAsync(character, now);
        });

        gm.MapPost("/item", async (GmItemRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            if (!ItemCatalog.TryGet(req.TemplateId, out var template)) throw GameErrors.NotFound("아이템 템플릿");
            if (req.Quantity < 1 || req.Quantity > (template.Stackable ? ItemCatalog.MaxStack : InventoryRules.Capacity))
                throw GameErrors.Validation("수량이 올바르지 않습니다.");
            if (req.EnhanceLevel < 0 || req.EnhanceLevel > config.Enhance.MaxLevel) throw GameErrors.Validation("강화 수치가 올바르지 않습니다.");

            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var items = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                var plan = GameOps.Plan(items, template.Id, req.Quantity);
                if (!plan.Fits) throw GameErrors.Conflict(ErrorCodes.InventoryFull);
                db.AddNewItems(OwnerKind.Character, character.Id, items, plan, template.Id, template.IsEquipment ? req.EnhanceLevel : 0, now);
            });
            return await db.InventoryDtoAsync(character);
        });

        gm.MapPost("/level", async (GmLevelRequest req, HttpContext http, GameDbContext db, TimeProvider clock) =>
        {
            if (req.Level < 1 || req.Level > ExpTable.MaxLevel) throw GameErrors.Validation($"레벨은 1~{ExpTable.MaxLevel} 사이여야 합니다.");
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                character.Level = req.Level;
                character.Exp = 0;
            });
            return await db.CharacterDtoAsync(character, now);
        });

        gm.MapPost("/enhance-rate", (GmEnhanceRateRequest req, RuntimeGameConfig config) =>
        {
            if (req.Reset || req.Rates == null)
            {
                config.SetEnhanceRates(null);
                return config.Enhance;
            }
            foreach (var r in req.Rates)
            {
                var sum = r.Success + r.Fail + r.Destroy;
                if (r.TargetLevel < 1 || r.TargetLevel > config.Enhance.MaxLevel || r.Success < 0 || r.Fail < 0 || r.Destroy < 0 || Math.Abs(sum - 1) > 0.0001)
                    throw GameErrors.Validation($"+{r.TargetLevel} 확률이 올바르지 않습니다. 성공+실패+파괴 = 1 이어야 합니다.");
            }
            // 지정한 수치만 바꾸고 나머지는 현재 값을 유지한다.
            var merged = config.Enhance.Rates.Where(r => req.Rates.All(n => n.TargetLevel != r.TargetLevel)).Concat(req.Rates)
                .OrderBy(r => r.TargetLevel).ToList();
            config.SetEnhanceRates(merged);
            return config.Enhance;
        });

        gm.MapPost("/bot/reset", async (GmBotResetRequest req, GameDbContext db, BotService bots) =>
        {
            var count = 0;
            await db.InTransactionAsync(async () => count = await bots.ResetAsync(req.Zone));
            return new GmBotResetResponse { BotCount = count };
        });

        gm.MapPost("/alignment", async (GmAlignmentRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var next = AlignmentRules.Clamp(config.Alignment, req.Value);
                db.RecordAlignment(EntityKind.Character, character.Id, next - character.Alignment, next, "GM", now);
                character.Alignment = next;
                if (req.ClearPurple) character.PurpleUntil = null;
            });
            return await db.CharacterDtoAsync(character, now);
        });

        gm.MapPost("/drop-rate", (GmDropRateRequest req, RuntimeGameConfig config) =>
        {
            static bool Valid(double? p) => p is null or (>= 0 and <= 1);
            if (!Valid(req.BotLootChance) || !Valid(req.BotLootChanceChaotic) || req.DeathDrop?.Any(t => !Valid(t.Chance)) == true)
                throw GameErrors.Validation("확률은 0~1 사이여야 합니다.");
            config.SetDrop(req.BotLootChance, req.BotLootChanceChaotic, req.DeathDrop, req.Reset);
            return config.Drop;
        });

        gm.MapGet("/config", (RuntimeGameConfig config) => new GmConfigResponse { Enhance = config.Enhance, Drop = config.Drop });
    }
}
