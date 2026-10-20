// 통합 테스트용 HTTP 클라이언트 래퍼 (JSON 직렬화, 인증·캐릭터 헤더, 에러 응답 검증)
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Xunit;

namespace Ashveil.Server.Tests.Infrastructure;

public sealed class GameClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public HttpClient Http { get; } = http;
    public string Username { get; private set; } = "";
    public string Password { get; } = "password1";
    public TokenResponse? Tokens { get; private set; }

    public async Task SignupAndLoginAsync()
    {
        Username = "u" + Guid.NewGuid().ToString("N")[..10];
        await PostAsync<SignupResponse>("/auth/signup", new SignupRequest { Username = Username, Password = Password });
        await LoginAsync();
    }

    public async Task LoginAsync()
    {
        Tokens = await PostAsync<TokenResponse>("/auth/login", new LoginRequest { Username = Username, Password = Password });
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Tokens.AccessToken);
    }

    public Task<CharacterDto> CreateCharacterAsync(ClassType classType = ClassType.Warrior, string? name = null) =>
        PostAsync<CharacterDto>("/characters", new CreateCharacterRequest
        {
            Name = name ?? "T" + Guid.NewGuid().ToString("N")[..8],
            ClassType = classType,
            Gender = Gender.Male,
        });

    public void UseCharacter(long characterId)
    {
        Http.DefaultRequestHeaders.Remove("X-Character-Id");
        Http.DefaultRequestHeaders.Add("X-Character-Id", characterId.ToString());
    }

    public async Task<T> GetAsync<T>(string url)
    {
        var response = await Http.GetAsync(url);
        return await ReadAsync<T>(response);
    }

    public async Task<T> PostAsync<T>(string url, object body)
    {
        var response = await Http.PostAsJsonAsync(url, body, Json);
        return await ReadAsync<T>(response);
    }

    public async Task PutAsync(string url, object body)
    {
        var response = await Http.PutAsJsonAsync(url, body, Json);
        if (!response.IsSuccessStatusCode) Assert.Fail($"PUT {url} 실패 {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>실패해야 하는 요청을 보내고 HTTP 상태와 에러 코드를 확인한다.</summary>
    public async Task<ErrorResponse> PostErrorAsync(string url, object body, HttpStatusCode status, string code)
    {
        var response = await Http.PostAsJsonAsync(url, body, Json);
        return await ExpectErrorAsync(response, status, code);
    }

    public async Task<ErrorResponse> GetErrorAsync(string url, HttpStatusCode status, string code) =>
        await ExpectErrorAsync(await Http.GetAsync(url), status, code);

    public static async Task<ErrorResponse> ExpectErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"기대 {(int)status}, 실제 {(int)response.StatusCode}: {raw}");
        var error = JsonSerializer.Deserialize<ErrorResponse>(raw, Json)!;
        Assert.Equal(code, error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        return error;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} 실패 {(int)response.StatusCode}: {raw}");
        return JsonSerializer.Deserialize<T>(raw, Json)!;
    }

    // 자주 쓰는 호출 묶음
    public Task<CharacterDto> MeAsync(long id) => GetAsync<CharacterDto>($"/characters/{id}");
    public Task<InventoryDto> InventoryAsync(long id) => GetAsync<InventoryDto>($"/characters/{id}/inventory");
    public Task<List<LedgerEntryDto>> LedgerAsync() => GetAsync<List<LedgerEntryDto>>("/currency/ledger?limit=200");
    public Task<CharacterDto> GmGoldAsync(long amount) => PostAsync<CharacterDto>("/gm/gold", new GmGoldRequest { Amount = amount });
    public Task<InventoryDto> GmItemAsync(string templateId, int quantity = 1, int enhance = 0) =>
        PostAsync<InventoryDto>("/gm/item", new GmItemRequest { TemplateId = templateId, Quantity = quantity, EnhanceLevel = enhance });
    public Task<CharacterDto> GmAlignmentAsync(int value) => PostAsync<CharacterDto>("/gm/alignment", new GmAlignmentRequest { Value = value });
}
