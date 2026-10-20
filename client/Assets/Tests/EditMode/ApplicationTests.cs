// Application 계층(JSON 호환, API 클라이언트 토큰 갱신·에러 변환, 서버 주소 설정, 플레이어 상태) EditMode 테스트
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ashveil.Application;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using NUnit.Framework;

namespace Ashveil.Tests
{
    /// <summary>경로별 응답을 돌려주는 가짜 서버.</summary>
    public sealed class FakeTransport : IHttpTransport
    {
        public readonly List<HttpRequestData> Requests = new List<HttpRequestData>();
        public Func<HttpRequestData, Task<HttpResponseData>> Handler = _ => Task.FromResult(Ok("{}"));

        public Task<HttpResponseData> SendAsync(HttpRequestData request)
        {
            Requests.Add(request);
            return Handler(request);
        }

        public static HttpResponseData Ok(string body) => new HttpResponseData { Status = 200, Body = body };
        public static HttpResponseData Error(long status, string code) =>
            new HttpResponseData { Status = status, Body = "{\"code\":\"" + code + "\",\"message\":\"server text\"}" };
    }

    public class ApplicationTests
    {
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // 테스트 스레드에서 await 연속 작업을 바로 실행하도록 Unity 동기화 컨텍스트를 잠시 뺀다.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        [TearDown]
        public void TearDown() => SynchronizationContext.SetSynchronizationContext(_context);

        private static (ApiClient client, Session session, FakeTransport transport) NewClient()
        {
            var transport = new FakeTransport();
            var session = new Session { AccessToken = "old-access", RefreshToken = "old-refresh", CharacterId = 7 };
            return (new ApiClient(transport, session, () => "http://server:8080/"), session, transport);
        }

        [Test]
        public void ServerJson_IsReadWithCamelCaseEnumsAndDates()
        {
            const string json = "{\"id\":3,\"name\":\"용사\",\"classType\":\"Archer\",\"gender\":\"Female\",\"level\":5,\"exp\":20,\"expToNext\":1500," +
                                "\"gold\":1234,\"position\":{\"x\":1.5,\"y\":2,\"z\":3},\"zone\":\"Beginner\",\"stats\":{\"maxHp\":126,\"maxMp\":52,\"attack\":35,\"defense\":12}," +
                                "\"alignment\":{\"value\":-300,\"state\":\"Chaotic\",\"purpleUntil\":\"2026-10-21T12:00:00Z\"},\"pkKills\":1,\"pkDeaths\":0}";
            var c = ClientJson.Deserialize<CharacterDto>(json);
            Assert.AreEqual(ClassType.Archer, c.ClassType);
            Assert.AreEqual(1234, c.Gold);
            Assert.AreEqual(ZoneId.Beginner, c.Zone);
            Assert.AreEqual(AlignmentState.Chaotic, c.Alignment.State);
            Assert.AreEqual(new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc), c.Alignment.PurpleUntil);
            Assert.AreEqual(1.5f, c.Position.X);
        }

        [Test]
        public void Requests_AreCamelCaseWithEnumNames()
        {
            var json = ClientJson.Serialize(new PlayerDeathRequest { KillerType = EntityKind.Bot, KillerId = 9, X = 1, Z = 2 });
            StringAssert.Contains("\"killerType\":\"Bot\"", json);
            StringAssert.Contains("\"killerId\":9", json);
            var pickup = ClientJson.Serialize(new PickupRequest { X = 1, Z = 1 });
            StringAssert.DoesNotContain("botId", pickup, "null 값은 보내지 않는다");
        }

        [Test]
        public void Headers_IncludeTokenAndCharacter()
        {
            var (client, _, transport) = NewClient();
            client.SendAsync<InventoryDto>("GET", "/characters/7/inventory").GetAwaiter().GetResult();
            var request = transport.Requests[0];
            Assert.AreEqual("http://server:8080/characters/7/inventory", request.Url);
            Assert.AreEqual("Bearer old-access", request.Headers["Authorization"]);
            Assert.AreEqual("7", request.Headers[ApiClient.CharacterHeader]);
        }

        [Test]
        public void Unauthorized_RefreshesOnce_AndRetries()
        {
            var (client, session, transport) = NewClient();
            transport.Handler = r =>
            {
                if (r.Url.EndsWith("/auth/refresh")) return Task.FromResult(FakeTransport.Ok("{\"accessToken\":\"new-access\",\"refreshToken\":\"new-refresh\",\"expiresInSeconds\":1800}"));
                var authorized = r.Headers.TryGetValue("Authorization", out var h) && h == "Bearer new-access";
                return Task.FromResult(authorized ? FakeTransport.Ok("{\"gold\":5}") : FakeTransport.Error(401, ErrorCodes.Unauthorized));
            };
            var inventory = client.SendAsync<InventoryDto>("GET", "/characters/7/inventory").GetAwaiter().GetResult();
            Assert.AreEqual(5, inventory.Gold);
            Assert.AreEqual("new-refresh", session.RefreshToken);
            Assert.AreEqual(3, transport.Requests.Count);
        }

        [Test]
        public void ConcurrentUnauthorized_ShareOneRefresh()
        {
            var (client, _, transport) = NewClient();
            var refresh = new TaskCompletionSource<HttpResponseData>();
            transport.Handler = r =>
            {
                if (r.Url.EndsWith("/auth/refresh")) return refresh.Task;
                var authorized = r.Headers.TryGetValue("Authorization", out var h) && h == "Bearer new-access";
                return Task.FromResult(authorized ? FakeTransport.Ok("{}") : FakeTransport.Error(401, ErrorCodes.Unauthorized));
            };
            var a = client.SendAsync<object>("GET", "/a");
            var b = client.SendAsync<object>("GET", "/b");
            refresh.SetResult(FakeTransport.Ok("{\"accessToken\":\"new-access\",\"refreshToken\":\"r2\",\"expiresInSeconds\":1800}"));
            Task.WaitAll(a, b);
            Assert.AreEqual(1, transport.Requests.FindAll(r => r.Url.EndsWith("/auth/refresh")).Count);
        }

        [Test]
        public void ErrorBody_BecomesApiExceptionWithKoreanMessage()
        {
            var (client, _, transport) = NewClient();
            transport.Handler = _ => Task.FromResult(FakeTransport.Error(409, ErrorCodes.InsufficientGold));
            var e = Assert.Throws<ApiException>(() => client.SendAsync<InventoryDto>("POST", "/shop/buy", new BuyRequest()).GetAwaiter().GetResult());
            Assert.AreEqual(ErrorCodes.InsufficientGold, e.Code);
            Assert.AreEqual(409, e.Status);
            Assert.AreEqual("골드가 부족합니다.", e.Message);
        }

        [Test]
        public void NetworkFailure_BecomesNetworkError()
        {
            var (client, _, transport) = NewClient();
            transport.Handler = _ => Task.FromResult(new HttpResponseData { NetworkError = true, Error = "Cannot connect" });
            var e = Assert.Throws<ApiException>(() => client.SendAsync<object>("GET", "/health", auth: false).GetAwaiter().GetResult());
            Assert.AreEqual(ErrorCodes.NetworkError, e.Code);
        }

        [Test]
        public void ServerSettings_RuntimeFileBeatsBuildDefault()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ashveil-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Assert.AreEqual(ServerSettings.FallbackUrl, new ServerSettings(dir, null).BaseUrl);
                var build = new ServerSettings(dir, "{\"baseUrl\":\"http://10.0.0.5:8080/\"}");
                Assert.AreEqual("http://10.0.0.5:8080", build.BaseUrl);
                Assert.AreEqual("build", build.Source);

                Assert.IsTrue(build.Save("192.168.0.20:8080"));
                var runtime = new ServerSettings(dir, "{\"baseUrl\":\"http://10.0.0.5:8080\"}");
                Assert.AreEqual("http://192.168.0.20:8080", runtime.BaseUrl);
                Assert.AreEqual("runtime", runtime.Source);
                Assert.IsFalse(runtime.Save("   "));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void PlayerState_KeepsGoldInSync_AndClampsVitals()
        {
            var state = new PlayerState();
            var changes = 0;
            state.CharacterChanged += () => changes++;
            state.Apply(new CharacterDto { Gold = 100, Stats = new StatsDto { MaxHp = 50, MaxMp = 20 }, Alignment = new AlignmentDto() });
            Assert.AreEqual(50, state.Hp);
            state.Apply(new InventoryDto { Gold = 70, Items = new List<ItemDto> { new ItemDto { Id = 1, TemplateId = ItemCatalog.PotionHpSmall, Quantity = 3 } } });
            Assert.AreEqual(70, state.Gold, "인벤토리 응답의 골드도 화면 골드에 반영한다");
            Assert.AreEqual(2, changes);
            Assert.AreEqual(3, state.CountOf(ItemCatalog.PotionHpSmall));

            state.Damage(80);
            Assert.AreEqual(0, state.Hp);
            Assert.IsTrue(state.IsDead);
            state.RestoreVitals();
            state.Heal(999, 999);
            Assert.AreEqual(50, state.Hp);
            Assert.AreEqual(20, state.Mp);
            Assert.IsFalse(state.TrySpendMp(21));
        }
    }
}
