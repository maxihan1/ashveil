// 몬스터 처치 보상, 봇 처치 전리품, PK 선공 판정, 플레이어 사망 처리 API
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Server.Services;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Endpoints;

public static class CombatEndpoints
{
    public static void MapCombatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/combat").WithTags("Combat").RequireAuthorization();

        group.MapPost("/monster-kill", async (MonsterKillRequest req, HttpContext http, GameDbContext db,
            RuntimeGameConfig config, IRandomSource random, TimeProvider clock) =>
        {
            GameOps.RequireFinite(req.X, req.Z);
            if (!SpawnTable.TryGet(req.SpawnId, out var spawn) || spawn.Monster != req.MonsterId)
                throw GameErrors.Conflict(ErrorCodes.InvalidSpawn);
            var info = spawn.Info;
            var range = info.LeashRadius + config.Combat.KillRangeTolerance;
            if (Distance(req.X, req.Z, spawn.X, spawn.Z) > range) throw GameErrors.Conflict(ErrorCodes.OutOfRange);

            var now = clock.GetUtcNow().UtcDateTime;
            var response = new MonsterKillResponse();
            Character character = null!;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());

                // (캐릭터, 스폰)별 리젠 쿨다운. 캐릭터 행 잠금 안이라 동시 요청도 한 번만 통과한다.
                var cooldown = await db.SpawnCooldowns.SingleOrDefaultAsync(s => s.CharacterId == character.Id && s.SpawnId == spawn.Id);
                var minGap = TimeSpan.FromSeconds(Math.Max(0, info.RespawnSeconds - config.Combat.RespawnToleranceSeconds));
                if (cooldown != null && now - cooldown.LastKilledAt < minGap) throw GameErrors.Conflict(ErrorCodes.Cooldown);
                if (cooldown == null) db.SpawnCooldowns.Add(new SpawnCooldown { CharacterId = character.Id, SpawnId = spawn.Id, LastKilledAt = now });
                else cooldown.LastKilledAt = now;

                var gold = random.Next(info.GoldMin, info.GoldMax + 1);
                db.ChangeGold(character, gold, LedgerReasons.MonsterKill, "Spawn", spawn.Id, now);

                var exp = ExpTable.ApplyExp(character.Level, character.Exp, info.Exp);
                character.Level = exp.Level;
                character.Exp = exp.Exp;
                character.Alignment = AlignmentRules.ApplyMonsterKill(config.Alignment, character.Alignment);

                response.GoldGained = gold;
                response.ExpGained = info.Exp;
                response.LeveledUp = exp.LeveledUp;
            });
            response.Character = await db.CharacterDtoAsync(character, now);
            return response;
        });

        group.MapPost("/bot-kill", async (BotKillRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config,
            IRandomSource random, TimeProvider clock, BotService bots) =>
        {
            GameOps.RequireFinite(req.X, req.Z);
            if (ZoneMap.IsSafeZone(ZoneMap.GetZone(req.X, req.Z))) throw GameErrors.Conflict(ErrorCodes.SafeZone);

            var now = clock.GetUtcNow().UtcDateTime;
            var drop = config.Drop;
            var response = new BotKillResponse();
            Character character = null!;
            Bot bot = null!;
            List<Item> botItems = null!;
            Item? landed = null;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                bot = await db.LockBotAsync(req.BotId) ?? throw GameErrors.NotFound("봇");
                if (bot.IsDead && bot.RespawnAt <= now)
                {
                    await bots.ReviveAsync(bot, now);
                    await db.SaveChangesAsync(); // 보충한 아이템이 아래 조회에 보이도록 먼저 쓴다(커밋은 아님)
                }
                if (bot.IsDead) throw GameErrors.Conflict(ErrorCodes.BotDead);
                if (!GameOps.IsNearZone(bot.Zone, req.X, req.Z, config.Combat.ZoneTolerance)) throw GameErrors.Conflict(ErrorCodes.OutOfRange);

                // 성향 판정: 선량한 봇을 죽이면 처치자 성향치가 떨어진다.
                var botState = AlignmentRules.GetState(bot.Alignment, bot.PurpleUntil, now);
                var outcome = AlignmentRules.ApplyKill(config.Alignment, character.Alignment, botState);
                character.Alignment = outcome.NewAlignment;
                character.PkKills += 1;
                db.RecordAlignment(EntityKind.Character, character.Id, outcome.Delta, outcome.NewAlignment, "PK_KILL_INNOCENT", now);
                db.PkRecords.Add(new PkRecord
                {
                    KillerKind = EntityKind.Character, KillerId = character.Id, KillerName = character.Name,
                    VictimKind = EntityKind.Bot, VictimId = bot.Id, VictimName = bot.Name,
                    VictimWasInnocent = outcome.VictimWasInnocent, AlignmentDelta = outcome.Delta, CreatedAt = now,
                });
                response.AlignmentDelta = outcome.Delta;

                bot.IsDead = true;
                bot.Deaths += 1;
                bot.PurpleUntil = null;
                bot.RespawnAt = now.AddSeconds(config.Bots.RespawnSeconds);

                // 전리품 판정과 이전을 같은 트랜잭션에서 한다.
                botItems = await db.LoadItemsAsync(OwnerKind.Bot, bot.Id);
                var roll = DropRules.RollBotLoot(drop, botState, random);
                if (!roll.Drops) return;
                var candidates = botItems.Select(i => new DropCandidate(i.Id, i.EquippedSlot != null)).ToList();
                var pickedId = DropRules.PickCandidate(candidates, roll.IncludeEquipped, random);
                if (pickedId == null) return;

                var item = botItems.First(i => i.Id == pickedId);
                botItems.Remove(item);
                var characterItems = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                var plan = GameOps.Plan(characterItems, item.TemplateId, item.Quantity);
                if (plan.Fits)
                {
                    landed = db.MoveItem(item, OwnerKind.Character, character.Id, characterItems, plan);
                }
                else
                {
                    // 인벤토리가 가득 차면 버리지 않고 처치 위치 바닥에 떨어뜨린다.
                    var ground = db.DropToGround(item, req.X, req.Z, EntityKind.Bot, bot.Id, drop.GroundItemLifetimeSeconds, now);
                    landed = item;
                    response.DroppedToGround = true;
                    await db.SaveChangesAsync();
                    response.GroundItemId = ground.ItemId;
                }
            });

            response.Dropped = landed == null ? null : Map.Item(landed);
            response.Character = await db.CharacterDtoAsync(character, now);
            response.Bot = Map.Bot(bot, botItems, now);
            return response;
        });

        group.MapPost("/pk-attack", async (PkAttackRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config, TimeProvider clock) =>
        {
            GameOps.RequireFinite(req.X, req.Z);
            var characterId = http.CharacterId();
            var playerAttacks = req.AttackerKind == EntityKind.Character && req.AttackerId == characterId && req.TargetKind == EntityKind.Bot;
            var botAttacks = req.AttackerKind == EntityKind.Bot && req.TargetKind == EntityKind.Character && req.TargetId == characterId;
            if (!playerAttacks && !botAttacks) throw GameErrors.Conflict(ErrorCodes.InvalidTarget);
            if (ZoneMap.IsSafeZone(ZoneMap.GetZone(req.X, req.Z))) throw GameErrors.Conflict(ErrorCodes.SafeZone);

            var now = clock.GetUtcNow().UtcDateTime;
            var response = new PkAttackResponse();
            await db.InTransactionAsync(async () =>
            {
                var character = await db.LockOwnedCharacterAsync(characterId, http.AccountId());
                var bot = await db.LockBotAsync(playerAttacks ? req.TargetId : req.AttackerId) ?? throw GameErrors.NotFound("봇");
                if (bot.IsDead) throw GameErrors.Conflict(ErrorCodes.BotDead);

                if (playerAttacks)
                {
                    var until = AlignmentRules.PurpleUntilAfterAttack(config.Alignment, AlignmentRules.GetState(bot.Alignment, bot.PurpleUntil, now), now);
                    response.AttackerBecamePurple = until != null && character.Alignment >= 0;
                    if (until != null && (character.PurpleUntil == null || character.PurpleUntil < until)) character.PurpleUntil = until;
                    response.Attacker = Map.Alignment(character.Alignment, character.PurpleUntil, now);
                    response.Target = Map.Alignment(bot.Alignment, bot.PurpleUntil, now);
                }
                else
                {
                    var until = AlignmentRules.PurpleUntilAfterAttack(config.Alignment, AlignmentRules.GetState(character.Alignment, character.PurpleUntil, now), now);
                    response.AttackerBecamePurple = until != null && bot.Alignment >= 0;
                    if (until != null && (bot.PurpleUntil == null || bot.PurpleUntil < until)) bot.PurpleUntil = until;
                    response.Attacker = Map.Alignment(bot.Alignment, bot.PurpleUntil, now);
                    response.Target = Map.Alignment(character.Alignment, character.PurpleUntil, now);
                }
            });
            return response;
        });

        group.MapPost("/player-death", async (PlayerDeathRequest req, HttpContext http, GameDbContext db, RuntimeGameConfig config,
            IRandomSource random, TimeProvider clock) =>
        {
            GameOps.RequireFinite(req.X, req.Z);
            if (req.KillerType == EntityKind.Character) throw GameErrors.Validation("killerType 은 Monster, Bot, Guard 중 하나여야 합니다.");
            if (req.KillerType == EntityKind.Bot && req.KillerId == null) throw GameErrors.Validation("봇에게 죽었다면 killerId 가 필요합니다.");

            var now = clock.GetUtcNow().UtcDateTime;
            var drop = config.Drop;
            var response = new PlayerDeathResponse();
            Character character = null!;
            GroundItem? dropped = null;
            await db.InTransactionAsync(async () =>
            {
                character = await db.LockOwnedCharacterAsync(http.CharacterId(), http.AccountId());
                var victimState = AlignmentRules.GetState(character.Alignment, character.PurpleUntil, now);

                if (req.KillerType == EntityKind.Bot)
                {
                    var bot = await db.LockBotAsync(req.KillerId!.Value) ?? throw GameErrors.NotFound("봇");
                    var outcome = AlignmentRules.ApplyKill(config.Alignment, bot.Alignment, victimState);
                    bot.Alignment = outcome.NewAlignment;
                    bot.Kills += 1;
                    character.PkDeaths += 1;
                    db.RecordAlignment(EntityKind.Bot, bot.Id, outcome.Delta, outcome.NewAlignment, "PK_KILL_INNOCENT", now);
                    db.PkRecords.Add(new PkRecord
                    {
                        KillerKind = EntityKind.Bot, KillerId = bot.Id, KillerName = bot.Name,
                        VictimKind = EntityKind.Character, VictimId = character.Id, VictimName = character.Name,
                        VictimWasInnocent = outcome.VictimWasInnocent, AlignmentDelta = outcome.Delta, CreatedAt = now,
                    });
                }

                response.ExpLost = ExpTable.DeathExpLoss(character.Level, character.Exp);
                character.Exp -= response.ExpLost;

                // 성향 기반 사망 드랍: 악명일수록 확률이 높고 장착 장비도 떨어질 수 있다.
                var roll = DropRules.RollDeathDrop(drop, character.Alignment, random);
                if (roll.Drops)
                {
                    var items = await db.LoadItemsAsync(OwnerKind.Character, character.Id);
                    var pickedId = DropRules.PickCandidate(
                        items.Select(i => new DropCandidate(i.Id, i.EquippedSlot != null)).ToList(), roll.IncludeEquipped, random);
                    if (pickedId != null)
                        dropped = db.DropToGround(items.First(i => i.Id == pickedId), req.X, req.Z, EntityKind.Character,
                            character.Id, drop.GroundItemLifetimeSeconds, now);
                }

                character.PurpleUntil = null;
                character.PosX = ZoneMap.RespawnX;
                character.PosY = 0;
                character.PosZ = ZoneMap.RespawnZ;
            });

            response.Dropped = dropped == null ? null : Map.Ground(dropped);
            response.Character = await db.CharacterDtoAsync(character, now);
            return response;
        });
    }

    private static float Distance(float x1, float z1, float x2, float z2) => MathF.Sqrt((x1 - x2) * (x1 - x2) + (z1 - z2) * (z1 - z2));
}
