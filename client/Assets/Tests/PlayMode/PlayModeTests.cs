// PlayMode 테스트: 가짜 서버로 로그인→캐릭터 선택 흐름, 서버 골드 응답의 HUD 반영, 몬스터 어그로·추적
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ashveil.Application;
using Ashveil.Presentation;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Ashveil.Tests
{
    /// <summary>경로별로 미리 정한 JSON을 돌려주는 가짜 서버.</summary>
    public sealed class FakeServer : IHttpTransport
    {
        public readonly Dictionary<string, object> Routes = new Dictionary<string, object>();
        public readonly List<string> Calls = new List<string>();

        public Task<HttpResponseData> SendAsync(HttpRequestData request)
        {
            var path = request.Url.Substring(request.Url.IndexOf('/', "http://".Length));
            var key = request.Method + " " + path.Split('?')[0];
            Calls.Add(key);
            return Task.FromResult(Routes.TryGetValue(key, out var body)
                ? new HttpResponseData { Status = 200, Body = ClientJson.Serialize(body) }
                : new HttpResponseData { Status = 404, Body = "{\"code\":\"NOT_FOUND\",\"message\":\"no route " + key + "\"}" });
        }
    }

    public class PlayModeTests
    {
        private FakeServer _server;

        private static CharacterDto Character(long gold = 500) => new CharacterDto
        {
            Id = 7, Name = "테스터", ClassType = ClassType.Warrior, Gender = Gender.Male, Level = 3, Exp = 10, ExpToNext = 600, Gold = gold,
            Position = new Vector3Dto { X = 100, Y = 0, Z = 140 }, Zone = ZoneId.Beginner,
            Stats = new StatsDto { MaxHp = 144, MaxMp = 34, Attack = 24, Defense = 8 },
            Alignment = new AlignmentDto { Value = 0, State = AlignmentState.Innocent },
        };

        [SetUp]
        public void SetUp()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            _server = new FakeServer();
            GameContext.Initialize(new ServerSettings(UnityEngine.Application.temporaryCachePath, "{\"baseUrl\":\"http://fake.local\"}"), _server);
        }

        private static readonly string[] CreatedRoots =
            { "Boot", "AppRoot", "ToastCanvas", "EventSystem", "Player", "HudCanvas", "NameplateCanvas", "Main Camera", "JoystickTestCanvas" };

        [TearDown]
        public void TearDown()
        {
            // 테스트 러너 자신의 오브젝트는 건드리지 않도록 이 테스트가 만든 루트만 지운다.
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == null || go.transform.parent != null) continue;
                if (System.Array.IndexOf(CreatedRoots, go.name) >= 0 || go.name.StartsWith("Monster_")) Object.Destroy(go);
            }
            WorldServices.Clear();
            GameContext.Shutdown();
        }

        [UnityTest]
        public IEnumerator Login_ShowsCharacterSelect_WithServerCharacters()
        {
            _server.Routes["POST /auth/login"] = new TokenResponse { AccessToken = "a", RefreshToken = "r", ExpiresInSeconds = 1800 };
            _server.Routes["GET /characters"] = new List<CharacterSummaryDto>
            {
                new CharacterSummaryDto { Id = 7, Name = "테스터", ClassType = ClassType.Archer, Level = 4, Zone = ZoneId.Town },
            };

            new GameObject("Boot").AddComponent<BootController>();
            yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<LoginView>(), "로그인 화면이 떠야 한다");

            GameObject.Find("UsernameInput").GetComponent<InputField>().text = "tester01";
            GameObject.Find("PasswordInput").GetComponent<InputField>().text = "secret1";
            GameObject.Find("LoginButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            yield return null;

            Assert.IsTrue(GameContext.Session.IsLoggedIn);
            Assert.IsNotNull(Object.FindFirstObjectByType<CharacterSelectView>(), "캐릭터 선택 화면으로 넘어가야 한다");
            var slot0 = GameObject.Find("CharacterSlot0");
            Assert.IsNotNull(slot0);
            Assert.AreEqual("테스터", slot0.transform.Find("NameText").GetComponent<Text>().text);
            Assert.IsNotNull(GameObject.Find("CharacterSlot1").transform.Find("CreateSlotButton"), "빈 슬롯에는 생성 버튼");
        }

        [UnityTest]
        public IEnumerator Hud_ShowsServerGold_AfterEveryResponse()
        {
            GameContext.Session.AccessToken = "a";
            GameContext.Session.CharacterId = 7;
            GameContext.Player.Apply(Character(), fillVitals: true);
            GameContext.Player.Apply(new InventoryDto { Gold = 500, Capacity = 40 });
            WorldServices.Player = PlayerController.Spawn(Character(), null);
            var hud = HudView.Create(null);
            WorldServices.Hud = hud;
            yield return null;

            var gold = GameObject.Find("GoldText").GetComponent<Text>();
            Assert.AreEqual("500", gold.text);

            GameContext.Player.Apply(Character(1234));
            Assert.AreEqual("1,234", gold.text, "캐릭터 응답의 골드가 바로 반영되어야 한다");

            GameContext.Player.Apply(new InventoryDto { Gold = 99, Capacity = 40 });
            Assert.AreEqual("99", gold.text, "인벤토리 응답의 골드도 반영되어야 한다");
        }

        [UnityTest]
        public IEnumerator Joystick_CenterIsNeutral_AndLeftMeansWest()
        {
            var canvas = Ui.CreateCanvas("JoystickTestCanvas", 0);
            var joystick = VirtualJoystick.Create(canvas.transform);
            yield return null;

            var rect = (RectTransform)joystick.transform;
            var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var data = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = center };
            joystick.OnPointerDown(data);
            Assert.Less(joystick.Value.magnitude, 0.05f, "패드 한가운데는 정지");

            data.position = center + new Vector2(-600, 0);
            joystick.OnDrag(data);
            Assert.Less(joystick.Value.x, -0.9f, "왼쪽으로 끌면 서쪽");
            Assert.AreEqual(0f, joystick.Value.y, 0.05f);

            data.position = center + new Vector2(0, -600);
            joystick.OnDrag(data);
            Assert.Less(joystick.Value.y, -0.9f, "아래로 끌면 남쪽(마을 방향)");

            joystick.OnPointerUp(data);
            Assert.AreEqual(Vector2.zero, joystick.Value);
        }

        [UnityTest]
        public IEnumerator Monster_ChasesPlayerInsideAggroRange()
        {
            GameContext.Player.Apply(Character(), fillVitals: true);
            SpawnTable.TryGet("B01", out var spawn);
            var character = Character();
            character.Position = new Vector3Dto { X = spawn.X + 6, Z = spawn.Z };
            WorldServices.Player = PlayerController.Spawn(character, null);
            var monster = MonsterController.Create(spawn, null);
            var start = Vector3.Distance(monster.transform.position, WorldServices.Player.transform.position);

            for (var i = 0; i < 20; i++) yield return null;

            Assert.AreNotEqual(Ashveil.Domain.MonsterState.Wander, monster.State);
            Assert.Less(Vector3.Distance(monster.transform.position, WorldServices.Player.transform.position), start);
        }

        [Test]
        public void BotNameplate_ShowsNameThenLevel()
        {
            // 기획서 §9: 봇 머리 위에는 「이름 Lv.N」
            var bot = new GameObject("Bot_Nameplate").AddComponent<Actor>();
            bot.Init(Team.Bot, "미릴온", 5, 100, 10, 5);
            try { Assert.AreEqual("미릴온 Lv.5", NameplateLayer.LabelOf(bot)); }
            finally { Object.Destroy(bot.gameObject); }
        }
    }
}
