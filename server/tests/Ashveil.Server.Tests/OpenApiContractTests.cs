// OpenAPI 문서(/openapi/v1.json)가 실제 응답의 상태 코드 · 본문 모양과 맞는지 주요 엔드포인트로 확인하는 테스트
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Ashveil.Server.Endpoints;
using Ashveil.Server.Tests.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests;

/// <summary>Test Commander 「응답 형식 검사」와 같은 기준: 받은 상태 코드가 문서에 있고, 본문이 그 코드의 스키마에 맞아야 한다.</summary>
public class OpenApiContractTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    private const float FieldX = 100f, FieldZ = 150f; // 초급 사냥터 좌표
    private JsonObject? _doc;

    [Fact]
    public async Task EveryOperation_DocumentsSuccessBodyAndErrors()
    {
        var doc = await DocAsync();
        var problems = new List<string>();
        foreach (var (path, operations) in doc["paths"]!.AsObject())
        foreach (var (method, operation) in operations!.AsObject())
        {
            var responses = operation!["responses"]!.AsObject();
            var label = $"{method.ToUpperInvariant()} {path}";
            foreach (var (code, response) in responses)
                if (code.StartsWith('2') && code != "204" && response!["content"]?["application/json"]?["schema"] == null)
                    problems.Add($"{label}: {code} 응답에 본문 스키마가 없다");
            if (path != "/health" && !responses.Any(r => r.Key.StartsWith('4'))) problems.Add($"{label}: 오류 응답이 없다");
            if (!path.StartsWith("/auth/") && path != "/health" && !responses.ContainsKey("401")) problems.Add($"{label}: 401 이 없다");
            foreach (var (code, response) in responses.Where(r => r.Key.StartsWith('4')))
                if (response!["content"]?["application/json"]?["schema"]?["$ref"]?.GetValue<string>() != "#/components/schemas/ErrorResponse")
                    problems.Add($"{label}: {code} 본문이 ErrorResponse 가 아니다");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public async Task Auth_MatchesDocument()
    {
        var http = server.NewClient().Http;
        var username = "oa" + Guid.NewGuid().ToString("N")[..10];
        var signup = new SignupRequest { Username = username, Password = "password1" };

        await CheckAsync("post", "/auth/signup", await http.PostAsJsonAsync("/auth/signup", signup), 201);
        await CheckAsync("post", "/auth/signup", await http.PostAsJsonAsync("/auth/signup", signup), 409);
        await CheckAsync("post", "/auth/signup", await http.PostAsJsonAsync("/auth/signup", new SignupRequest { Username = "A!", Password = "password1" }), 400);
        await CheckAsync("post", "/auth/signup", await http.PostAsync("/auth/signup", new StringContent("{", Encoding.UTF8, "application/json")), 400);
        await CheckAsync("post", "/auth/login", await http.PostAsJsonAsync("/auth/login", new LoginRequest { Username = username, Password = "wrong-pw" }), 401);
        await CheckAsync("post", "/auth/login", await http.PostAsJsonAsync("/auth/login", new LoginRequest { Username = username, Password = "password1" }), 200);
        await CheckAsync("post", "/auth/refresh", await http.PostAsJsonAsync("/auth/refresh", new RefreshRequest { RefreshToken = "nope" }), 401);
        await CheckAsync("get", "/characters", await http.GetAsync("/characters"), 401);
    }

    [Fact]
    public async Task Characters_MatchDocument()
    {
        var client = server.NewClient();
        await client.SignupAndLoginAsync("oa");
        var http = client.Http;
        var name = "O" + Guid.NewGuid().ToString("N")[..8];
        var create = new CreateCharacterRequest { Name = name, ClassType = ClassType.Warrior, Gender = Gender.Female };

        var created = await CheckAsync("post", "/characters", await http.PostAsJsonAsync("/characters", create, GameClient.Json), 201);
        var id = created!["id"]!.GetValue<long>();
        await CheckAsync("post", "/characters", await http.PostAsJsonAsync("/characters", create, GameClient.Json), 409);
        await CheckAsync("get", "/characters", await http.GetAsync("/characters"), 200);
        await CheckAsync("get", "/characters/{id}", await http.GetAsync($"/characters/{id}"), 200);
        await CheckAsync("get", "/characters/{id}", await http.GetAsync("/characters/999999999"), 404);
        var (other, otherCharacter) = await server.NewPlayerAsync();
        await CheckAsync("get", "/characters/{id}", await other.Http.GetAsync($"/characters/{id}"), 403);

        // 시작 아이템: 장착한 무기 · 갑옷과 장착 칸이 null 인 물약
        var inventory = await CheckAsync("get", "/characters/{id}/inventory", await http.GetAsync($"/characters/{id}/inventory"), 200);
        Assert.Contains(inventory!["items"]!.AsArray(), i => i!["equippedSlot"] == null);
        var weaponId = inventory["items"]!.AsArray().First(i => i!["equippedSlot"]?.GetValue<string>() == "Weapon")!["id"]!.GetValue<long>();
        await CheckAsync("post", "/characters/{id}/unequip",
            await http.PostAsJsonAsync($"/characters/{id}/unequip", new EquipRequest { ItemId = weaponId }), 200);
        await CheckAsync("post", "/characters/{id}/equip",
            await http.PostAsJsonAsync($"/characters/{id}/equip", new EquipRequest { ItemId = weaponId }), 200);
        await CheckAsync("put", "/characters/{id}/position",
            await http.PutAsJsonAsync($"/characters/{id}/position", new PositionRequest { X = 50, Y = 0, Z = 50 }), 204);
        await CheckAsync("get", "/characters/{id}/pk-history", await http.GetAsync($"/characters/{id}/pk-history"), 200);
        await CheckAsync("delete", "/characters/{id}", await other.Http.DeleteAsync($"/characters/{id}"), 403);
        await CheckAsync("delete", "/characters/{id}", await http.DeleteAsync($"/characters/{id}"), 204);
        Assert.True(otherCharacter.Id > 0);
    }

    [Fact]
    public async Task Gameplay_NullableObjectsMatchDocument()
    {
        var (client, character) = await server.NewPlayerAsync();
        var http = client.Http;
        var admin = server.NewAdminClient();

        await CheckAsync("get", "/shop/items", await http.GetAsync("/shop/items"), 200);
        await CheckAsync("post", "/shop/buy", await http.PostAsJsonAsync("/shop/buy", new BuyRequest { TemplateId = ItemCatalog.PotionHpSmall, Quantity = 1 }), 200);
        await CheckAsync("post", "/shop/buy", await http.PostAsJsonAsync("/shop/buy", new BuyRequest { TemplateId = "armor_chain", Quantity = 1 }), 409);
        var noCharacter = server.NewClient();
        noCharacter.Http.DefaultRequestHeaders.Authorization = http.DefaultRequestHeaders.Authorization;
        await CheckAsync("get", "/shop/items", await noCharacter.Http.GetAsync("/shop/items"), 400);
        await CheckAsync("get", "/currency/ledger", await http.GetAsync("/currency/ledger?limit=5"), 200);
        await CheckAsync("get", "/world/spawns", await http.GetAsync("/world/spawns"), 200);

        // 강화 파괴: item 이 null
        await client.GmGoldAsync(5000);
        await client.GmItemAsync("helm_iron", 1, enhance: 3);
        var bag = await client.GmItemAsync(ItemCatalog.ScrollArmor, 1);
        await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest { Enhance = [EnhanceResult.Destroy] });
        var destroyed = await CheckAsync("post", "/enhance", await http.PostAsJsonAsync("/enhance", new EnhanceRequest
        {
            ItemId = bag.Items.Single(i => i.TemplateId == "helm_iron").Id,
            ScrollId = bag.Items.Single(i => i.TemplateId == ItemCatalog.ScrollArmor).Id,
        }), 200);
        Assert.Null(destroyed!["item"]);

        // 봇 처치 전리품 없음: dropped 가 null
        var bots = await client.GetAsync<List<BotDto>>("/bots?zone=Beginner");
        await CheckAsync("get", "/bots", await http.GetAsync("/bots?zone=Beginner"), 200);
        var alive = bots.Where(b => !b.IsDead).ToList();
        await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest { BotLoot = [false] });
        var kill = await CheckAsync("post", "/combat/bot-kill",
            await http.PostAsJsonAsync("/combat/bot-kill", new BotKillRequest { BotId = alive[0].Id, X = FieldX, Z = FieldZ }), 200);
        Assert.Null(kill!["dropped"]);
        await CheckAsync("post", "/combat/bot-kill",
            await http.PostAsJsonAsync("/combat/bot-kill", new BotKillRequest { BotId = 999999999, X = FieldX, Z = FieldZ }), 404);

        // 사망 드랍 없음: dropped 가 null / 드랍 있음 → 봇이 주우면 inventory 가 null
        await admin.PostAsync<AdminForcedDto>("/admin/forced", new AdminForceRequest { DeathDrop = [false, true] });
        var death = new PlayerDeathRequest { KillerType = EntityKind.Monster, X = FieldX, Z = FieldZ };
        var noDrop = await CheckAsync("post", "/combat/player-death", await http.PostAsJsonAsync("/combat/player-death", death, GameClient.Json), 200);
        Assert.Null(noDrop!["dropped"]);
        var withDrop = await CheckAsync("post", "/combat/player-death", await http.PostAsJsonAsync("/combat/player-death", death, GameClient.Json), 200);
        var groundId = withDrop!["dropped"]!["id"]!.GetValue<long>();
        await CheckAsync("get", "/world/ground-items", await http.GetAsync("/world/ground-items?zone=Beginner"), 200);
        var picked = await CheckAsync("post", "/world/ground-items/{id}/pickup", await http.PostAsJsonAsync($"/world/ground-items/{groundId}/pickup",
            new PickupRequest { BotId = alive[1].Id, X = FieldX, Z = FieldZ }), 200);
        Assert.Null(picked!["inventory"]);
        Assert.True(character.Id > 0);
    }

    private async Task<JsonObject> DocAsync() =>
        _doc ??= JsonNode.Parse(await server.NewClient().Http.GetStringAsync("/openapi/v1.json"))!.AsObject();

    /// <summary>기대한 상태 코드인지 보고, 그 코드가 문서에 있으며 본문이 문서 스키마에 맞는지 본다. 본문(JSON)을 돌려준다.</summary>
    private async Task<JsonNode?> CheckAsync(string method, string path, HttpResponseMessage response, int expected)
    {
        var doc = await DocAsync();
        var body = await response.Content.ReadAsStringAsync();
        var label = $"{method.ToUpperInvariant()} {path}";
        Assert.True((int)response.StatusCode == expected, $"{label}: 기대 {expected}, 실제 {(int)response.StatusCode} {body}");

        var responses = doc["paths"]![path]![method]!["responses"]!.AsObject();
        Assert.True(responses.ContainsKey(expected.ToString()),
            $"{label}: 문서에 없는 응답 코드 {expected} (문서: {string.Join(", ", responses.Select(r => r.Key))})");
        var schema = responses[expected.ToString()]!["content"]?["application/json"]?["schema"];
        if (schema == null)
        {
            Assert.True(body.Length == 0, $"{label} {expected}: 문서에는 본문이 없는데 본문이 왔다 {body}");
            return null;
        }
        var json = JsonNode.Parse(body);
        var errors = new List<string>();
        Validate(doc, schema, json, "$", errors);
        Assert.True(errors.Count == 0, $"{label} {expected}: 본문이 문서와 맞지 않는다\n{string.Join("\n", errors)}\n{body}");
        return json;
    }

    /// <summary>생성된 문서가 쓰는 JSON Schema(2020-12) 키워드만 검사한다. oneOf 는 규격대로 정확히 한 갈래만 맞아야 한다.</summary>
    private static void Validate(JsonObject doc, JsonNode schema, JsonNode? value, string at, List<string> errors)
    {
        foreach (var (keyword, rule) in schema.AsObject())
        {
            switch (keyword)
            {
                case "$ref":
                    var name = rule!.GetValue<string>().Split('/')[^1];
                    Validate(doc, doc["components"]!["schemas"]![name]!, value, at, errors);
                    break;
                case "type":
                    var types = rule is JsonArray list ? list.Select(t => t!.GetValue<string>()).ToList() : [rule!.GetValue<string>()];
                    var actual = TypeOf(value);
                    if (!types.Contains(actual) && !(actual == "integer" && types.Contains("number")))
                        errors.Add($"{at}: 타입이 다름 (문서 {string.Join("|", types)} · 받은 값 {actual})");
                    break;
                case "enum":
                    if (!rule!.AsArray().Any(e => JsonNode.DeepEquals(e, value))) errors.Add($"{at}: 열거값 밖 {value?.ToJsonString() ?? "null"}");
                    break;
                case "oneOf":
                    var matched = rule!.AsArray().Count(branch =>
                    {
                        var branchErrors = new List<string>();
                        Validate(doc, branch!, value, at, branchErrors);
                        return branchErrors.Count == 0;
                    });
                    if (matched != 1) errors.Add($"{at}: oneOf 갈래 {matched}개가 맞음 (정확히 1개여야 한다)");
                    break;
                case "properties":
                    if (value is JsonObject obj)
                        foreach (var (property, propertySchema) in rule!.AsObject())
                            if (obj.TryGetPropertyValue(property, out var child)) Validate(doc, propertySchema!, child, $"{at}.{property}", errors);
                    break;
                case "required":
                    if (value is JsonObject required)
                        foreach (var property in rule!.AsArray().Select(p => p!.GetValue<string>()).Where(p => !required.ContainsKey(p)))
                            errors.Add($"{at}: {property} 칸이 없다");
                    break;
                case "items":
                    if (value is JsonArray array)
                        for (var i = 0; i < array.Count; i++) Validate(doc, rule!, array[i], $"{at}[{i}]", errors);
                    break;
                case "pattern":
                    if (value is JsonValue text && text.GetValueKind() == JsonValueKind.String && !Regex.IsMatch(text.GetValue<string>(), rule!.GetValue<string>()))
                        errors.Add($"{at}: pattern 에 안 맞음");
                    break;
                case "format": // 주석(annotation) 키워드라 검사하지 않는다
                case "description":
                    break;
                default:
                    errors.Add($"{at}: 이 검사기가 모르는 키워드 {keyword} — 검사기에 더한다");
                    break;
            }
        }
    }

    private static string TypeOf(JsonNode? value) => value switch
    {
        null => "null",
        JsonObject => "object",
        JsonArray => "array",
        _ => value.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            JsonValueKind.Number => value.GetValue<double>() % 1 == 0 ? "integer" : "number",
            _ => "null",
        },
    };
}
