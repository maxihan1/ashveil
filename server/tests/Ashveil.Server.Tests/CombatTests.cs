// 몬스터 처치 보상·검증(쿨다운·거리·스폰)과 원장 일치, 플레이어 사망 처리 통합 테스트
using System.Net;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

public class CombatTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private static MonsterKillRequest KillAt(string spawnId)
    {
        SpawnTable.TryGet(spawnId, out var spawn);
        return new MonsterKillRequest { MonsterId = spawn.Monster, SpawnId = spawn.Id, X = spawn.X + 3, Z = spawn.Z - 2 };
    }

    [Fact]
    public async Task MonsterKill_GrantsGoldAndExp_ThenCooldownBlocksRepeat()
    {
        var (client, character) = await server.NewPlayerAsync();
        server.Random.Enqueue(0.0); // 골드 최소값
        var result = await client.PostAsync<MonsterKillResponse>("/combat/monster-kill", KillAt("B01"));

        Assert.Equal(8, result.GoldGained);
        Assert.Equal(20, result.ExpGained);
        Assert.Equal(508, result.Character.Gold);
        Assert.Equal(20, result.Character.Exp);

        await client.PostErrorAsync("/combat/monster-kill", KillAt("B01"), HttpStatusCode.Conflict, ErrorCodes.Cooldown);
        await client.PostAsync<MonsterKillResponse>("/combat/monster-kill", KillAt("B02")); // 다른 스폰은 된다

        var ledger = await client.LedgerAsync();
        Assert.Equal(2, ledger.Count(e => e.Reason == LedgerReasons.MonsterKill));
        Assert.Equal((await client.MeAsync(character.Id)).Gold, ledger.Sum(e => e.Delta));
    }

    [Fact]
    public async Task MonsterKill_RejectsUnknownSpawn_WrongType_AndFarPosition()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.PostErrorAsync("/combat/monster-kill", new MonsterKillRequest { MonsterId = MonsterType.Imp, SpawnId = "NOPE", X = 0, Z = 0 },
            HttpStatusCode.Conflict, ErrorCodes.InvalidSpawn);

        var wrongType = KillAt("B03");
        wrongType.MonsterId = MonsterType.ImpBoss;
        await client.PostErrorAsync("/combat/monster-kill", wrongType, HttpStatusCode.Conflict, ErrorCodes.InvalidSpawn);

        var far = KillAt("B03");
        far.X += 60;
        await client.PostErrorAsync("/combat/monster-kill", far, HttpStatusCode.Conflict, ErrorCodes.OutOfRange);
    }

    [Fact]
    public async Task MonsterKill_LevelsUp_AndRecoversAlignment()
    {
        var (client, _) = await server.NewPlayerAsync();
        await client.GmAlignmentAsync(-35);
        MonsterKillResponse last = null!;
        foreach (var spawn in new[] { "B04", "B05", "B06", "B07", "B08" })
            last = await client.PostAsync<MonsterKillResponse>("/combat/monster-kill", KillAt(spawn));

        Assert.True(last.LeveledUp);
        Assert.Equal(2, last.Character.Level);
        Assert.Equal(0, last.Character.Exp);
        Assert.Equal(15, last.Character.Alignment.Value);
        Assert.Equal(AlignmentState.Innocent, last.Character.Alignment.State);
    }

    [Fact]
    public async Task ConcurrentKillsOfSameSpawn_OnlyOnePays()
    {
        var (client, character) = await server.NewPlayerAsync();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            client.Http.PostAsync("/combat/monster-kill", System.Net.Http.Json.JsonContent.Create(KillAt("B09"), options: GameClient.Json))));
        Assert.Equal(1, responses.Count(r => r.IsSuccessStatusCode));
        Assert.Single(await client.LedgerAsync(), e => e.Reason == LedgerReasons.MonsterKill);
        _ = character;
    }

    [Fact]
    public async Task PlayerDeath_LosesExp_RespawnsInTown()
    {
        var (client, character) = await server.NewPlayerAsync();
        foreach (var spawn in new[] { "B10", "B11", "B12" })
            await client.PostAsync<MonsterKillResponse>("/combat/monster-kill", KillAt(spawn));
        await client.PutAsync($"/characters/{character.Id}/position", new PositionRequest { X = 100, Z = 150 });

        server.Random.Enqueue(0.99); // 선량 5% 드랍 → 드랍 없음
        var death = await client.PostAsync<PlayerDeathResponse>("/combat/player-death",
            new PlayerDeathRequest { KillerType = EntityKind.Monster, X = 100, Z = 150 });

        Assert.Equal(10, death.ExpLost); // 레벨1 필요 경험치 100의 10%
        Assert.Equal(50, death.Character.Exp);
        Assert.Null(death.Dropped);
        Assert.Equal(ZoneId.Town, death.Character.Zone);
        Assert.Equal(ZoneMap.RespawnX, death.Character.Position.X);
    }

    [Fact]
    public async Task Ledger_BalancesAlwaysMatchGold()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.PostAsync<MonsterKillResponse>("/combat/monster-kill", KillAt("B13"));
        await client.PostAsync<InventoryDto>("/shop/buy", new BuyRequest { TemplateId = ItemCatalog.PotionMpSmall, Quantity = 2 });
        await client.GmGoldAsync(77);

        var ledger = await client.LedgerAsync(); // 최신순
        var gold = (await client.MeAsync(character.Id)).Gold;
        Assert.Equal(gold, ledger.First().BalanceAfter);
        Assert.Equal(gold, ledger.Sum(e => e.Delta));
        var ordered = ledger.OrderBy(e => e.Id).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.Equal(ordered[i - 1].BalanceAfter + ordered[i].Delta, ordered[i].BalanceAfter);
    }
}
