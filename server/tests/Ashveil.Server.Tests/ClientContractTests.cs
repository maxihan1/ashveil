// 클라이언트(Unity, Newtonsoft) 직렬화 설정으로 실제 서버 응답을 읽어 DTO 계약이 맞는지 확인하는 테스트
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Ashveil.Server.Tests;

public class ClientContractTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    // client/Assets/Game/Application/Http.cs 의 ClientJson.Settings 와 같아야 한다.
    private static readonly JsonSerializerSettings Client = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,
    };

    private static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, raw);
        return JsonConvert.DeserializeObject<T>(raw, Client)!;
    }

    private static StringContent Body(object value) =>
        new(JsonConvert.SerializeObject(value, Client), System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task ClientSerializer_ReadsServerResponses()
    {
        var (client, created) = await server.NewPlayerAsync(ClassType.Mage);
        var http = client.Http;

        var character = await Read<CharacterDto>(await http.GetAsync($"/characters/{created.Id}"));
        Assert.Equal(ClassType.Mage, character.ClassType);
        Assert.Equal(ZoneId.Town, character.Zone);
        Assert.Equal(AlignmentState.Innocent, character.Alignment.State);
        Assert.Equal(80, character.Stats.MaxHp);

        var inventory = await Read<InventoryDto>(await http.GetAsync($"/characters/{created.Id}/inventory"));
        Assert.Contains(inventory.Items, i => i.EquippedSlot == EquipSlot.Weapon);
        Assert.Contains(inventory.Items, i => i.EquippedSlot == null);

        // 클라이언트가 보내는 요청 본문(Newtonsoft camelCase + enum 문자열)도 서버가 읽어야 한다.
        server.Random.Enqueue(0.0);
        SpawnTable.TryGet("B14", out var spawn);
        var kill = await Read<MonsterKillResponse>(await http.PostAsync("/combat/monster-kill",
            Body(new MonsterKillRequest { MonsterId = spawn.Monster, SpawnId = spawn.Id, X = spawn.X, Z = spawn.Z })));
        Assert.Equal(8, kill.GoldGained);

        var bots = await Read<List<BotDto>>(await http.GetAsync("/bots?zone=Beginner"));
        Assert.All(bots, b => Assert.Equal(ZoneId.Beginner, b.Zone));
        Assert.Contains(bots, b => b.Temperament == Temperament.Aggressive);

        await client.GmAlignmentAsync(-700);
        server.Random.Enqueue(0.0, 0.0);
        var death = await Read<PlayerDeathResponse>(await http.PostAsync("/combat/player-death",
            Body(new PlayerDeathRequest { KillerType = EntityKind.Monster, X = 100, Z = 150 })));
        Assert.NotNull(death.Dropped);
        Assert.True(death.Dropped!.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, death.Dropped.ExpiresAt.Kind);

        var error = JsonConvert.DeserializeObject<ErrorResponse>(
            await (await http.PostAsync("/shop/buy", Body(new BuyRequest { TemplateId = "armor_chain", Quantity = 99 }))).Content.ReadAsStringAsync(), Client)!;
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
    }
}
