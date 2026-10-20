// 인증(가입·로그인·토큰 회전)과 캐릭터(생성 규칙·위치 저장·장착·소유권) 통합 테스트
using System.Net;
using System.Net.Http.Json;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

public class AuthAndCharacterTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task Signup_Login_Refresh_RotatesRefreshToken()
    {
        var client = server.NewClient();
        await client.SignupAndLoginAsync();
        var first = client.Tokens!;

        var second = await client.PostAsync<TokenResponse>("/auth/refresh", new RefreshRequest { RefreshToken = first.RefreshToken });
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.Equal(1800, second.ExpiresInSeconds);

        // 이미 쓴 토큰은 다시 못 쓰고, 재사용이 감지되면 새 토큰까지 폐기된다.
        await client.PostErrorAsync("/auth/refresh", new RefreshRequest { RefreshToken = first.RefreshToken },
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
        await client.PostErrorAsync("/auth/refresh", new RefreshRequest { RefreshToken = second.RefreshToken },
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken);
    }

    [Fact]
    public async Task Login_WithWrongPassword_IsRejected()
    {
        var client = server.NewClient();
        await client.SignupAndLoginAsync();
        await client.PostErrorAsync("/auth/login", new LoginRequest { Username = client.Username, Password = "wrong-pass" },
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
        await client.PostErrorAsync("/auth/login", new LoginRequest { Username = "nobody_here", Password = "whatever" },
            HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials);
    }

    [Fact]
    public async Task Signup_DuplicateOrInvalid_IsRejected()
    {
        var client = server.NewClient();
        await client.SignupAndLoginAsync();
        await client.PostErrorAsync("/auth/signup", new SignupRequest { Username = client.Username, Password = "password2" },
            HttpStatusCode.Conflict, ErrorCodes.UsernameTaken);
        await client.PostErrorAsync("/auth/signup", new SignupRequest { Username = "No Spaces", Password = "password2" },
            HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        await client.PostErrorAsync("/auth/signup", new SignupRequest { Username = "validname", Password = "123" },
            HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ReturnsUnifiedError()
    {
        var client = server.NewClient();
        await client.GetErrorAsync("/characters", HttpStatusCode.Unauthorized, ErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task CreateCharacter_GivesStarterKit_AndLedgerEntry()
    {
        var (client, character) = await server.NewPlayerAsync(ClassType.Archer);
        Assert.Equal(1, character.Level);
        Assert.Equal(500, character.Gold);
        Assert.Equal(ZoneId.Town, character.Zone);
        Assert.Equal(AlignmentState.Innocent, character.Alignment.State);
        Assert.Equal(14 + 9, character.Stats.Attack);   // 궁수 기본 14 + 숏 보우 9
        Assert.Equal(4 + 4, character.Stats.Defense);   // 궁수 기본 4 + 가죽 갑옷 4

        var inventory = await client.InventoryAsync(character.Id);
        Assert.Equal(3, inventory.Used);
        Assert.Contains(inventory.Items, i => i.TemplateId == "bow_short" && i.EquippedSlot == EquipSlot.Weapon);
        Assert.Contains(inventory.Items, i => i.TemplateId == "armor_leather" && i.EquippedSlot == EquipSlot.Armor);
        Assert.Contains(inventory.Items, i => i.TemplateId == ItemCatalog.PotionHpSmall && i.Quantity == 10);

        var ledger = await client.LedgerAsync();
        var entry = Assert.Single(ledger);
        Assert.Equal(LedgerReasons.CharacterCreated, entry.Reason);
        Assert.Equal(500, entry.Delta);
        Assert.Equal(500, entry.BalanceAfter);
    }

    [Fact]
    public async Task FourthCharacter_HitsLimit_AndNamesAreUnique()
    {
        var client = server.NewClient();
        await client.SignupAndLoginAsync();
        var name = "N" + Guid.NewGuid().ToString("N")[..8];
        var first = await client.CreateCharacterAsync(name: name);
        await client.CreateCharacterAsync();
        await client.CreateCharacterAsync();

        await client.PostErrorAsync("/characters", new CreateCharacterRequest { Name = "Z" + Guid.NewGuid().ToString("N")[..8] },
            HttpStatusCode.Conflict, ErrorCodes.CharacterLimit);

        // 다른 계정이 같은 이름(대소문자 무시)을 쓰면 거부, 삭제하면 다시 쓸 수 있다.
        var other = server.NewClient();
        await other.SignupAndLoginAsync();
        await other.PostErrorAsync("/characters", new CreateCharacterRequest { Name = name.ToLowerInvariant() },
            HttpStatusCode.Conflict, ErrorCodes.NameTaken);

        var delete = await client.Http.DeleteAsync($"/characters/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var reused = await other.CreateCharacterAsync(name: name);
        Assert.Equal(name, reused.Name);

        var list = await client.GetAsync<List<CharacterSummaryDto>>("/characters");
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task Position_IsKeptAcrossRelogin()
    {
        var (client, character) = await server.NewPlayerAsync();
        await client.PutAsync($"/characters/{character.Id}/position", new PositionRequest { X = 120.5f, Y = 3.25f, Z = 150f });

        await client.LoginAsync(); // 재접속
        var me = await client.MeAsync(character.Id);
        Assert.Equal(120.5f, me.Position.X);
        Assert.Equal(3.25f, me.Position.Y);
        Assert.Equal(150f, me.Position.Z);
        Assert.Equal(ZoneId.Beginner, me.Zone);

        var response = await client.Http.PutAsJsonAsync($"/characters/{character.Id}/position", new PositionRequest { X = 999, Z = 10 });
        await GameClient.ExpectErrorAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task OtherAccountsCharacter_IsForbidden()
    {
        var (_, character) = await server.NewPlayerAsync();
        var (intruder, _) = await server.NewPlayerAsync();
        await intruder.GetErrorAsync($"/characters/{character.Id}", HttpStatusCode.Forbidden, ErrorCodes.Forbidden);

        intruder.UseCharacter(character.Id);
        await intruder.GetErrorAsync("/currency/ledger", HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task UseItem_ConsumesPotion_AndRejectsEquipment()
    {
        var (client, character) = await server.NewPlayerAsync();
        var inventory = await client.InventoryAsync(character.Id);
        var potion = inventory.Items.Single(i => i.TemplateId == ItemCatalog.PotionHpSmall);
        var weapon = inventory.Items.Single(i => i.EquippedSlot == EquipSlot.Weapon);

        var used = await client.PostAsync<UseItemResponse>($"/characters/{character.Id}/use-item", new UseItemRequest { ItemId = potion.Id });
        Assert.Equal(60, used.HealHp);
        Assert.Equal(9, used.Inventory.Items.Single(i => i.Id == potion.Id).Quantity);

        await client.PostErrorAsync($"/characters/{character.Id}/use-item", new UseItemRequest { ItemId = weapon.Id },
            HttpStatusCode.Conflict, ErrorCodes.NotUsable);
    }

    [Fact]
    public async Task Equip_SwapsItemInSameSlot_AndUpdatesStats()
    {
        var (client, character) = await server.NewPlayerAsync(ClassType.Warrior);
        var inventory = await client.GmItemAsync("sword_long", enhance: 2);
        var longSword = inventory.Items.Single(i => i.TemplateId == "sword_long");

        var equipped = await client.PostAsync<EquipResponse>($"/characters/{character.Id}/equip", new EquipRequest { ItemId = longSword.Id });
        Assert.Equal(12 + 14 + 4, equipped.Stats.Attack);
        Assert.Single(equipped.Inventory.Items, i => i.EquippedSlot == EquipSlot.Weapon);
        Assert.Null(equipped.Inventory.Items.Single(i => i.TemplateId == "sword_short").EquippedSlot);

        var potion = equipped.Inventory.Items.Single(i => i.TemplateId == ItemCatalog.PotionHpSmall);
        await client.PostErrorAsync($"/characters/{character.Id}/equip", new EquipRequest { ItemId = potion.Id },
            HttpStatusCode.Conflict, ErrorCodes.NotEquippable);

        var unequipped = await client.PostAsync<EquipResponse>($"/characters/{character.Id}/unequip", new EquipRequest { ItemId = longSword.Id });
        Assert.Equal(12, unequipped.Stats.Attack);
    }
}
