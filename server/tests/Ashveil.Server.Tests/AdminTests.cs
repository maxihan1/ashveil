// 어드민 API 통합 테스트: 키 인증, 캐릭터 조회·조작·초기화, 결과 고정, 테스트 계정 정리, 서버 관리 스폰 표
using System.Net;
using Ashveil.Server.Endpoints;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

public class AdminTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private const float FieldX = 100f, FieldZ = 150f;

    [Fact]
    public async Task WrongOrMissingKey_IsRejected()
    {
        await server.NewClient().GetErrorAsync("/admin/accounts", HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
        var wrong = server.NewClient();
        wrong.Http.DefaultRequestHeaders.Add(AdminEndpoints.KeyHeader, "not-the-key");
        await wrong.GetErrorAsync("/admin/accounts", HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task FindsAnyCharacter_AndReadsInventoryAndLedger()
    {
        var (_, character) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();

        var found = Assert.Single(await admin.GetAsync<List<AdminCharacterDto>>($"/admin/characters?name={character.Name}"));
        Assert.Equal(character.Id, found.Id);
        Assert.Equal(ZoneId.Town, found.Zone);
        Assert.Equal(ItemCatalog.StarterGold, found.Gold);

        var inventory = await admin.GetAsync<InventoryDto>($"/admin/characters/{character.Id}/inventory");
        Assert.Contains(inventory.Items, i => i.EquippedSlot == EquipSlot.Weapon);
        var ledger = await admin.GetAsync<List<LedgerEntryDto>>($"/admin/characters/{character.Id}/ledger");
        Assert.Equal(LedgerReasons.CharacterCreated, Assert.Single(ledger).Reason);

        await admin.GetErrorAsync("/admin/characters/999999", HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Patch_SetsGoldWithLedger_LevelAlignmentAndPosition()
    {
        var (client, character) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();

        var updated = await admin.SendAsync<CharacterDto>(HttpMethod.Patch, $"/admin/characters/{character.Id}", new AdminCharacterUpdate
        {
            Gold = 12345, Level = 10, Alignment = -700, X = 120, Z = 150,
        });
        Assert.Equal(12345, updated.Gold);
        Assert.Equal(10, updated.Level);
        Assert.Equal(AlignmentState.Chaotic, updated.Alignment.State);
        Assert.Equal(ZoneId.Beginner, updated.Zone);

        // 플레이어가 보는 값과 원장 합계가 같아야 한다.
        Assert.Equal(12345, (await client.MeAsync(character.Id)).Gold);
        var ledger = await client.LedgerAsync();
        Assert.Equal(12345 - ItemCatalog.StarterGold, ledger.Single(e => e.Reason == LedgerReasons.AdminAdjust).Delta);
        Assert.Equal(12345, ledger.Sum(e => e.Delta));

        await admin.SendErrorAsync(HttpMethod.Patch, $"/admin/characters/{character.Id}", new AdminCharacterUpdate { Gold = -1 },
            HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        await admin.SendErrorAsync(HttpMethod.Patch, $"/admin/characters/{character.Id}", new AdminCharacterUpdate { X = 10 },
            HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task GiveAndDeleteItem_ThenResetRestoresStarterState()
    {
        var (client, character) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();

        var inventory = await admin.PostAsync<InventoryDto>($"/admin/characters/{character.Id}/items",
            new GmItemRequest { TemplateId = "sword_long", Quantity = 1, EnhanceLevel = 5 });
        var sword = inventory.Items.Single(i => i.TemplateId == "sword_long");
        Assert.Equal(5, sword.EnhanceLevel);

        inventory = await admin.SendAsync<InventoryDto>(HttpMethod.Delete, $"/admin/characters/{character.Id}/items/{sword.Id}");
        Assert.DoesNotContain(inventory.Items, i => i.TemplateId == "sword_long");

        await admin.SendAsync<CharacterDto>(HttpMethod.Patch, $"/admin/characters/{character.Id}", new AdminCharacterUpdate { Gold = 9000, Level = 20, Alignment = 500 });
        await admin.PostAsync<InventoryDto>($"/admin/characters/{character.Id}/items", new GmItemRequest { TemplateId = "helm_iron", Quantity = 1 });

        var reset = await admin.PostAsync<CharacterDto>($"/admin/characters/{character.Id}/reset", new { });
        Assert.Equal(1, reset.Level);
        Assert.Equal(ItemCatalog.StarterGold, reset.Gold);
        Assert.Equal(0, reset.Alignment.Value);
        Assert.Equal(ZoneId.Town, reset.Zone);
        var items = (await client.InventoryAsync(character.Id)).Items;
        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, i => i.TemplateId == "helm_iron");
        Assert.Equal(ItemCatalog.StarterGold, (await client.LedgerAsync()).Sum(e => e.Delta));
    }

    [Fact]
    public async Task ForcedEnhanceResults_AreUsedInOrder_AboveSafeLevelOnly()
    {
        var (client, character) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();
        await admin.SendAsync<CharacterDto>(HttpMethod.Patch, $"/admin/characters/{character.Id}", new AdminCharacterUpdate { Gold = 100000 });
        await admin.PostAsync<InventoryDto>($"/admin/characters/{character.Id}/items", new GmItemRequest { TemplateId = "helm_iron", EnhanceLevel = 2, Quantity = 1 });
        var inventory = await admin.PostAsync<InventoryDto>($"/admin/characters/{character.Id}/items", new GmItemRequest { TemplateId = ItemCatalog.ScrollArmor, Quantity = 4 });
        var helm = inventory.Items.Single(i => i.TemplateId == "helm_iron");
        var scroll = inventory.Items.Single(i => i.TemplateId == ItemCatalog.ScrollArmor);

        var forced = await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest
        {
            Enhance = [EnhanceResult.Fail, EnhanceResult.Success, EnhanceResult.Destroy],
        });
        Assert.Equal(3, forced.Enhance.Count);

        server.Random.Enqueue(0.0); // 난수가 성공을 가리켜도 고정 결과가 이긴다
        var safe = await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id });
        Assert.Equal(EnhanceResult.Success, safe.Result); // +2 → +3 은 안전 구간이라 고정 결과를 쓰지 않는다
        Assert.Equal(3, (await admin.GetAsync<AdminForcedDto>("/admin/forced")).Enhance.Count);

        server.Random.Enqueue(0.0);
        Assert.Equal(EnhanceResult.Fail, (await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id })).Result);
        server.Random.Enqueue(0.9999);
        Assert.Equal(EnhanceResult.Success, (await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id })).Result);
        var destroyed = await client.PostAsync<EnhanceResponse>("/enhance", new EnhanceRequest { ItemId = helm.Id, ScrollId = scroll.Id });
        Assert.Equal(EnhanceResult.Destroy, destroyed.Result);
        Assert.Null(destroyed.Item);
        Assert.Empty((await admin.GetAsync<AdminForcedDto>("/admin/forced")).Enhance);

        var history = await admin.GetAsync<List<AdminEnhanceHistoryDto>>($"/admin/characters/{character.Id}/enhance-history");
        Assert.Equal([EnhanceResult.Destroy, EnhanceResult.Success, EnhanceResult.Fail, EnhanceResult.Success], history.Select(h => h.Result));
    }

    [Fact]
    public async Task ForcedBotLootAndDeathDrop_OverrideTheRoll()
    {
        var (client, character) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();
        var bot = (await client.GetAsync<List<BotDto>>("/bots?zone=Beginner")).First(b => !b.IsDead && b.Alignment.State == AlignmentState.Innocent);

        await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest { BotLoot = [false], DeathDrop = [true] });
        server.Random.Enqueue(0.0, 0.0); // 난수는 드랍을 가리키지만 고정 결과(드랍 없음)가 이긴다
        var kill = await client.PostAsync<BotKillResponse>("/combat/bot-kill", new BotKillRequest { BotId = bot.Id, X = FieldX, Z = FieldZ });
        Assert.Null(kill.Dropped);

        server.Random.Clear();
        server.Random.Default = 0.9999; // 선량 캐릭터 사망 드랍(5%)은 원래 안 나오는 값
        try
        {
            var death = await client.PostAsync<PlayerDeathResponse>("/combat/player-death",
                new PlayerDeathRequest { KillerType = EntityKind.Monster, X = FieldX, Z = FieldZ });
            Assert.NotNull(death.Dropped);
        }
        finally
        {
            server.Random.Default = 0.5;
        }
        Assert.Empty((await admin.SendAsync<AdminForcedDto>(HttpMethod.Delete, "/admin/forced")).DeathDrop);
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyPrefixedAccounts_AndRefusesShortPrefix()
    {
        var prefix = "qa" + Guid.NewGuid().ToString("N")[..4];
        var (_, doomed) = await server.NewPlayerAsync(usernamePrefix: prefix);
        await server.NewPlayerAsync(usernamePrefix: prefix);
        var (_, survivor) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();

        var result = await admin.PostAsync<AdminCleanupResponse>("/admin/cleanup", new AdminCleanupRequest { UsernamePrefix = prefix });
        Assert.Equal(2, result.AccountsDeleted);
        Assert.Equal(2, result.CharactersDeleted);
        await admin.GetErrorAsync($"/admin/characters/{doomed.Id}", HttpStatusCode.NotFound, ErrorCodes.NotFound);
        await admin.GetAsync<CharacterDto>($"/admin/characters/{survivor.Id}");

        await admin.PostErrorAsync("/admin/cleanup", new AdminCleanupRequest { UsernamePrefix = "u" }, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Spawns_AreServedFromServer_AndAdminEditsChangeKillValidation()
    {
        var (client, _) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();
        try
        {
            var spawns = await client.GetAsync<List<SpawnPointDto>>("/world/spawns");
            Assert.Equal(SpawnTable.All.Count, spawns.Count);

            var added = await admin.SendAsync<SpawnPointDto>(HttpMethod.Put, "/admin/spawns/T01",
                new AdminSpawnRequest { Zone = ZoneId.Beginner, Monster = MonsterType.VandalImp, X = 60, Z = 120 });
            Assert.Equal("T01", added.Id);
            Assert.Contains(await client.GetAsync<List<SpawnPointDto>>("/world/spawns"), s => s.Id == "T01");
            await client.PostAsync<MonsterKillResponse>("/combat/monster-kill",
                new MonsterKillRequest { MonsterId = MonsterType.VandalImp, SpawnId = "T01", X = 61, Z = 121 });

            await admin.SendAsync<AdminCountResponse>(HttpMethod.Delete, "/admin/spawns/B05");
            await client.PostErrorAsync("/combat/monster-kill", new MonsterKillRequest { MonsterId = MonsterType.Imp, SpawnId = "B05", X = 294, Z = 100 },
                HttpStatusCode.Conflict, ErrorCodes.InvalidSpawn);

            await admin.SendErrorAsync(HttpMethod.Put, "/admin/spawns/T02", new AdminSpawnRequest { Zone = ZoneId.Town, Monster = MonsterType.Imp, X = 200, Z = 40 },
                HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        }
        finally
        {
            var reset = await admin.PostAsync<AdminCountResponse>("/admin/spawns/reset", new { });
            Assert.Equal(SpawnTable.All.Count, reset.Count);
        }
    }

    [Fact]
    public async Task WorldReset_RemovesGroundItems()
    {
        var (client, _) = await server.NewPlayerAsync();
        var admin = server.NewAdminClient();
        await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest { DeathDrop = [true] });
        await client.PostAsync<PlayerDeathResponse>("/combat/player-death", new PlayerDeathRequest { KillerType = EntityKind.Monster, X = FieldX, Z = FieldZ });
        Assert.NotEmpty(await client.GetAsync<List<GroundItemDto>>("/world/ground-items"));

        var reset = await admin.PostAsync<AdminWorldResetResponse>("/admin/world/reset", new AdminWorldResetRequest());
        Assert.True(reset.GroundItemsDeleted >= 1);
        Assert.Equal(20, reset.BotCount);
        Assert.Empty(await client.GetAsync<List<GroundItemDto>>("/world/ground-items"));
    }
}

public class AdminDisabledTests(ProductionServerFixture server) : IClassFixture<ProductionServerFixture>
{
    [Fact]
    public async Task AdminApi_IsDisabledOutsideQa()
    {
        var admin = server.NewAdminClient();
        await admin.GetErrorAsync("/admin/accounts", HttpStatusCode.NotFound, ErrorCodes.AdminDisabled);
    }
}
