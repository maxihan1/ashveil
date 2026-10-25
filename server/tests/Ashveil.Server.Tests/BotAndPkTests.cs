// 봇 목록·처치 전리품, PK 선공(보라)·악명 판정, 안전지대, GM 봇 리셋 통합 테스트
using System.Net;
using System.Net.Http.Json;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

public class BotAndPkTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private const float FieldX = 100f, FieldZ = 150f; // 초급 사냥터 좌표

    private static async Task<BotDto> InnocentBotAsync(GameClient client, ZoneId zone = ZoneId.Beginner, int skip = 0)
    {
        var bots = await client.GetAsync<List<BotDto>>($"/bots?zone={zone}");
        return bots.Where(b => !b.IsDead && b.Alignment.State == AlignmentState.Innocent).Skip(skip).First();
    }

    [Fact]
    public async Task Bots_AreSeededPerZone_WithEquipment()
    {
        var (client, _) = await server.NewPlayerAsync();
        var bots = await client.GetAsync<List<BotDto>>("/bots");
        Assert.Equal(8, bots.Count(b => b.Zone == ZoneId.Beginner));
        Assert.Equal(10, bots.Count(b => b.Zone == ZoneId.Intermediate));
        Assert.Equal(2, bots.Count(b => b.Zone == ZoneId.Boss));
        Assert.All(bots, b => Assert.Contains(b.Equipment, i => i.EquippedSlot == EquipSlot.Weapon));
        // 장착 외 장비 2 + 소모품 2 (다른 테스트가 처치해 전리품 1개를 가져갔을 수 있다)
        Assert.All(bots, b => Assert.True(b.ItemCount >= b.Equipment.Count + 3));
        // 기획서 §9: 전체 20명 중 평화형 40%, 반격형 40%, 선공형 20%
        Assert.Equal(8, bots.Count(b => b.Temperament == Temperament.Peaceful));
        Assert.Equal(8, bots.Count(b => b.Temperament == Temperament.Retaliate));
        Assert.Equal(4, bots.Count(b => b.Temperament == Temperament.Aggressive));
        Assert.Equal(bots.Count, bots.Select(b => b.Name).Distinct().Count());
    }

    [Fact]
    public async Task KillingInnocentBot_MakesKillerChaotic_AndTransfersLoot()
    {
        var (client, character) = await server.NewPlayerAsync();
        var bot = await InnocentBotAsync(client);

        server.Random.Enqueue(0.0, 0.0); // 드랍 성공, 첫 번째 후보
        var result = await client.PostAsync<BotKillResponse>("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = FieldX, Z = FieldZ });

        Assert.Equal(-300, result.AlignmentDelta);
        Assert.Equal(AlignmentState.Chaotic, result.Character.Alignment.State);
        Assert.Equal(1, result.Character.PkKills);
        Assert.True(result.Bot.IsDead);
        Assert.NotNull(result.Bot.RespawnAt);
        Assert.NotNull(result.Dropped);
        Assert.False(result.DroppedToGround);
        Assert.Equal(bot.ItemCount - 1, result.Bot.ItemCount);

        var inventory = await client.InventoryAsync(character.Id);
        Assert.Contains(inventory.Items, i => i.TemplateId == result.Dropped!.TemplateId);

        await client.PostErrorAsync("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = FieldX, Z = FieldZ },
            HttpStatusCode.Conflict, ErrorCodes.BotDead);

        var history = await client.GetAsync<PkHistoryDto>($"/characters/{character.Id}/pk-history");
        var record = Assert.Single(history.Records);
        Assert.True(record.VictimWasInnocent);
        Assert.Equal(bot.Name, record.VictimName);
        Assert.Equal(-300, Assert.Single(history.AlignmentHistory).Delta);

        // 악명 캐릭터는 상점 가격이 오른다.
        Assert.Equal(1.2, (await client.GetAsync<ShopItemsResponse>("/shop/items")).PriceMultiplier);
    }

    [Fact]
    public async Task BotLoot_WithFullInventory_FallsToGroundInsteadOfVanishing()
    {
        var (client, character) = await server.NewPlayerAsync();
        var potions = (await client.InventoryAsync(character.Id)).Items.Single(i => i.TemplateId == ItemCatalog.PotionHpSmall);
        await client.PostAsync<InventoryDto>("/shop/sell", new SellRequest { ItemId = potions.Id, Quantity = potions.Quantity });
        await client.GmItemAsync("gloves_leather", InventoryRules.Capacity - 2);

        var bot = await InnocentBotAsync(client, skip: 1);
        server.Random.Enqueue(0.0, 0.0);
        var result = await client.PostAsync<BotKillResponse>("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = FieldX, Z = FieldZ });

        Assert.True(result.DroppedToGround);
        Assert.NotNull(result.GroundItemId);
        var ground = await client.GetAsync<List<GroundItemDto>>("/world/ground-items?zone=Beginner");
        var item = Assert.Single(ground, g => g.Id == result.GroundItemId);
        Assert.Equal(result.Dropped!.TemplateId, item.TemplateId);
        Assert.Equal(FieldX, item.X);
    }

    [Fact]
    public async Task AttackingInnocentBot_MakesPlayerPurple()
    {
        var (client, character) = await server.NewPlayerAsync();
        var bot = await InnocentBotAsync(client, skip: 2);

        var attack = await client.PostAsync<PkAttackResponse>("/combat/pk-attack", new PkAttackRequest
        {
            AttackerKind = EntityKind.Character, AttackerId = character.Id, TargetKind = EntityKind.Bot, TargetId = bot.Id, X = FieldX, Z = FieldZ,
        });
        Assert.True(attack.AttackerBecamePurple);
        Assert.Equal(AlignmentState.Purple, attack.Attacker.State);
        Assert.Equal(AlignmentState.Innocent, attack.Target.State);
        Assert.Equal(AlignmentState.Purple, (await client.MeAsync(character.Id)).Alignment.State);
    }

    [Fact]
    public async Task BotThatAttackedFirst_CanBeKilledWithoutPenalty()
    {
        var (client, character) = await server.NewPlayerAsync();
        var bot = await InnocentBotAsync(client, ZoneId.Intermediate);

        var attack = await client.PostAsync<PkAttackResponse>("/combat/pk-attack", new PkAttackRequest
        {
            AttackerKind = EntityKind.Bot, AttackerId = bot.Id, TargetKind = EntityKind.Character, TargetId = character.Id, X = 200, Z = 260,
        });
        Assert.Equal(AlignmentState.Purple, attack.Attacker.State);

        server.Random.Enqueue(0.99); // 전리품 없음
        var kill = await client.PostAsync<BotKillResponse>("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = 200, Z = 260 });
        Assert.Equal(0, kill.AlignmentDelta);
        Assert.Equal(AlignmentState.Innocent, kill.Character.Alignment.State);
        Assert.Null(kill.Dropped);
    }

    [Fact]
    public async Task SafeZone_BlocksPk_AndForeignAttackerIsRejected()
    {
        var (client, character) = await server.NewPlayerAsync();
        var bot = await InnocentBotAsync(client, ZoneId.Intermediate, skip: 1);

        await client.PostErrorAsync("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = 200, Z = 40 },
            HttpStatusCode.Conflict, ErrorCodes.SafeZone);
        await client.PostErrorAsync("/combat/pk-attack", new PkAttackRequest
        {
            AttackerKind = EntityKind.Character, AttackerId = character.Id, TargetKind = EntityKind.Bot, TargetId = bot.Id, X = 200, Z = 40,
        }, HttpStatusCode.Conflict, ErrorCodes.SafeZone);
        await client.PostErrorAsync("/combat/pk-attack", new PkAttackRequest
        {
            AttackerKind = EntityKind.Character, AttackerId = character.Id + 999, TargetKind = EntityKind.Bot, TargetId = bot.Id, X = 200, Z = 260,
        }, HttpStatusCode.Conflict, ErrorCodes.InvalidTarget);
        // 봇 구역에서 멀리 떨어진 곳(초급 남서쪽)에서 중급 봇을 처치했다고 보고하면 거부한다.
        await client.PostErrorAsync("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = 20, Z = 20 },
            HttpStatusCode.Conflict, ErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task GmBotReset_ReseedsZone()
    {
        var (client, _) = await server.NewPlayerAsync();
        var reset = await client.PostAsync<GmBotResetResponse>("/gm/bot/reset", new GmBotResetRequest { Zone = ZoneId.Boss });
        Assert.Equal(2, reset.BotCount);
        var bots = await client.GetAsync<List<BotDto>>("/bots?zone=Boss");
        Assert.All(bots, b => Assert.False(b.IsDead));
        Assert.Equal(2, bots.Count);
    }
}

/// <summary>5-1단계 완료 조건 시나리오: 악명 사망 → 바닥 드랍 → 봇이 주움 → 봇 처치로 되찾기.</summary>
public class DeathDropRecoveryTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task ChaoticDeathDrop_PickedUpByBot_IsRecoveredByKillingTheBot()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.GmAlignmentAsync(-700);

        // 1) 악명 상태로 사망 → 80% 드랍, 장착 장비 포함 → 첫 후보(장착 무기)가 바닥으로
        server.Random.Enqueue(0.0, 0.0);
        var death = await client.PostAsync<PlayerDeathResponse>("/combat/player-death",
            new PlayerDeathRequest { KillerType = EntityKind.Monster, X = 100, Z = 150 });
        var dropped = Assert.IsType<GroundItemDto>(death.Dropped);
        Assert.Equal(ItemCatalog.StarterWeapon(ClassType.Warrior), dropped.TemplateId);
        Assert.Equal(ZoneId.Beginner, dropped.Zone);
        var inventory = await client.InventoryAsync(character.Id);
        Assert.DoesNotContain(inventory.Items, i => i.Id == dropped.Id);
        Assert.Contains(await client.GetAsync<List<GroundItemDto>>("/world/ground-items?zone=Beginner"), g => g.Id == dropped.Id);

        // 2) 봇이 줍는다 → 봇 인벤토리로 들어간다
        var bots = await client.GetAsync<List<BotDto>>("/bots?zone=Beginner");
        var bot = bots.First(b => !b.IsDead);
        var pickup = await client.PostAsync<PickupResponse>($"/world/ground-items/{dropped.Id}/pickup",
            new PickupRequest { BotId = bot.Id, X = 100, Z = 150 });
        Assert.Equal(EntityKind.Bot, pickup.PickedByKind);
        Assert.Equal(bot.Id, pickup.PickedById);
        Assert.DoesNotContain(await client.GetAsync<List<GroundItemDto>>("/world/ground-items?zone=Beginner"), g => g.Id == dropped.Id);
        var botAfter = (await client.GetAsync<List<BotDto>>("/bots?zone=Beginner")).Single(b => b.Id == bot.Id);
        Assert.Equal(bot.ItemCount + 1, botAfter.ItemCount);

        // 3) 봇을 처치하면 그 아이템을 되찾을 수 있다 (전리품 확률 100%, 가장 최근 아이템 선택)
        try
        {
            await client.PostAsync<DropConfig>("/gm/drop-rate", new GmDropRateRequest { BotLootChance = 1.0 });
            server.Random.Enqueue(0.0, 0.9999);
            var kill = await client.PostAsync<BotKillResponse>("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = 100, Z = 150 });
            Assert.Equal(dropped.Id, kill.Dropped!.Id);
            Assert.Contains((await client.InventoryAsync(character.Id)).Items, i => i.Id == dropped.Id);
        }
        finally
        {
            await client.PostAsync<DropConfig>("/gm/drop-rate", new GmDropRateRequest { Reset = true });
        }
    }

    [Fact]
    public async Task SameGroundItem_PickedConcurrently_OnlyOnceSucceeds()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.GmAlignmentAsync(-700);
        server.Random.Enqueue(0.0, 0.99); // 드랍 성공, 마지막 후보(물약)
        var death = await client.PostAsync<PlayerDeathResponse>("/combat/player-death",
            new PlayerDeathRequest { KillerType = EntityKind.Monster, X = 300, Z = 150 });
        var dropped = death.Dropped!;

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.Http.PostAsJsonAsync($"/world/ground-items/{dropped.Id}/pickup", new PickupRequest { X = 300, Z = 151 }, GameClient.Json)));
        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        Assert.All(responses.Where(r => !r.IsSuccessStatusCode), r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task PickupFarAway_IsRejected()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.GmAlignmentAsync(-700);
        server.Random.Enqueue(0.0, 0.0);
        var death = await client.PostAsync<PlayerDeathResponse>("/combat/player-death",
            new PlayerDeathRequest { KillerType = EntityKind.Monster, X = 50, Z = 120 });
        await client.PostErrorAsync($"/world/ground-items/{death.Dropped!.Id}/pickup", new PickupRequest { X = 80, Z = 120 },
            HttpStatusCode.Conflict, ErrorCodes.OutOfRange);
    }
}

public class GmDisabledTests(ProductionServerFixture server) : IClassFixture<ProductionServerFixture>
{
    [Fact]
    public async Task GmEndpoints_AreDisabledOutsideQa()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.PostErrorAsync("/gm/gold", new GmGoldRequest { Amount = 100 }, HttpStatusCode.NotFound, ErrorCodes.GmDisabled);
        var swagger = await client.Http.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
    }
}
