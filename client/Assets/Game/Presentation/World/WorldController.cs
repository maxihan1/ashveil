// World 씬 조립: 캐릭터·인벤토리 로드 → 플레이어·카메라·HUD·몬스터·봇·NPC·바닥 아이템 시스템 생성
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashveil.Presentation
{
    /// <summary>World 씬 안의 시스템 접근점(씬이 내려가면 비운다).</summary>
    public static class WorldServices
    {
        public static PlayerController Player { get; internal set; }
        public static HudView Hud { get; internal set; }
        public static CombatReporter Reporter { get; internal set; }
        public static BotManager Bots { get; internal set; }
        public static GroundItemManager Ground { get; internal set; }
        public static PositionSaver PositionSaver { get; internal set; }
        public static MonsterSpawnSystem Monsters { get; internal set; }

        internal static void Clear()
        {
            Player = null;
            Hud = null;
            Reporter = null;
            Bots = null;
            Ground = null;
            PositionSaver = null;
            Monsters = null;
        }
    }

    public sealed class WorldController : MonoBehaviour
    {
        private async void Start()
        {
            AppRoot.EnsureExists();
            if (!GameContext.Session.IsLoggedIn || GameContext.Session.CharacterId <= 0)
            {
                SceneManager.LoadScene(SceneNames.Boot);
                return;
            }

            try
            {
                var character = await GameContext.Api.GetCharacterAsync();
                var inventory = await GameContext.Api.GetInventoryAsync();
                var spawns = await GameContext.Api.GetSpawnsAsync();
                if (this == null) return;
                GameContext.Player.Reset();
                GameContext.Player.Apply(character, fillVitals: true);
                GameContext.Player.Apply(inventory);
                Build(character, spawns);
            }
            catch (ApiException e)
            {
                Toast.Show(e.Message);
                SceneManager.LoadScene(SceneNames.Boot);
            }
        }

        private void Build(Shared.Dto.CharacterDto character, System.Collections.Generic.List<Shared.Dto.SpawnPointDto> spawns)
        {
            var systems = new GameObject("Systems");
            systems.transform.SetParent(transform, false);
            WorldServices.Reporter = systems.AddComponent<CombatReporter>();
            WorldServices.PositionSaver = systems.AddComponent<PositionSaver>();
            systems.AddComponent<ZoneTracker>();
            systems.AddComponent<TapTargeting>();
            systems.AddComponent<AutoHuntController>();

            var actors = new GameObject("Actors").transform;
            actors.SetParent(transform, false);
            WorldServices.Player = PlayerController.Spawn(character, actors);
            CameraRig.Create(WorldServices.Player.transform);

            NameplateLayer.Create(transform);
            WorldServices.Hud = HudView.Create(transform);

            var monsters = new GameObject("Monsters");
            monsters.transform.SetParent(transform, false);
            WorldServices.Monsters = monsters.AddComponent<MonsterSpawnSystem>();
            WorldServices.Monsters.Init(spawns);

            var bots = new GameObject("Bots");
            bots.transform.SetParent(transform, false);
            WorldServices.Bots = bots.AddComponent<BotManager>();

            var ground = new GameObject("GroundItems");
            ground.transform.SetParent(transform, false);
            WorldServices.Ground = ground.AddComponent<GroundItemManager>();

            var npcs = new GameObject("Npcs").transform;
            npcs.SetParent(transform, false);
            foreach (var placement in WorldLayout.Npcs) NpcController.Create(placement, npcs);

            Toast.Show($"{ZoneMap.DisplayName(character.Zone)}에 들어왔습니다.");
        }

        private void OnDestroy() => WorldServices.Clear();
    }

    /// <summary>주기적으로(5초, 1m 이상 이동 시)·앱 일시정지·종료 시 위치를 서버에 저장한다.</summary>
    public sealed class PositionSaver : MonoBehaviour
    {
        private const float IntervalSeconds = 5f;
        private Vector3 _lastSaved = new Vector3(float.NaN, 0, 0);
        private float _timer = IntervalSeconds;
        private bool _saving;

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = IntervalSeconds;
            Save(force: false);
        }

        public void SaveNow() => Save(force: true);

        private void Save(bool force)
        {
            var player = WorldServices.Player;
            if (player == null || player.Actor.IsDead || _saving) return;
            var p = player.transform.position;
            if (!force && !float.IsNaN(_lastSaved.x) && Vector3.Distance(p, _lastSaved) < 1f) return;
            _saving = true;
            _lastSaved = p;
            AsyncUi.Run(async () =>
            {
                try
                {
                    await GameContext.Api.SavePositionAsync(p.x, p.y, p.z);
                }
                finally
                {
                    _saving = false;
                }
            }, e => Debug.LogWarning("[Position] 저장 실패: " + e.Code));
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();
        }

        private void OnApplicationQuit() => SaveNow();
    }

    /// <summary>구역 경계를 넘으면 구역명 토스트를 띄운다.</summary>
    public sealed class ZoneTracker : MonoBehaviour
    {
        private ZoneId? _current;

        public static event System.Action<ZoneId> ZoneChanged;

        private void Update()
        {
            if (Time.frameCount % 10 != 0) return;
            var player = WorldServices.Player;
            if (player == null) return;
            var zone = WorldGround.ZoneAt(player.transform.position);
            if (_current == zone) return;
            var first = _current == null;
            _current = zone;
            ZoneChanged?.Invoke(zone);
            if (first) return;
            Toast.Show(ZoneMap.IsSafeZone(zone)
                ? $"{ZoneMap.DisplayName(zone)} (안전지대)"
                : $"{ZoneMap.DisplayName(zone)}에 들어섰습니다.");
        }
    }
}
