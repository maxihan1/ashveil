// 몬스터 스폰 지점 고정 표 (서버 처치 검증과 클라이언트 배치가 함께 쓴다)
using System.Collections.Generic;

namespace Ashveil.Shared
{
    public sealed class SpawnPoint
    {
        public string Id { get; }
        public ZoneId Zone { get; }
        public MonsterType Monster { get; }
        public float X { get; }
        public float Z { get; }

        public SpawnPoint(string id, ZoneId zone, MonsterType monster, float x, float z)
        {
            Id = id; Zone = zone; Monster = monster; X = x; Z = z;
        }

        public MonsterInfo Info => MonsterCatalog.Get(Monster);
    }

    public static class SpawnTable
    {
        // 좌표는 결정적 해시로 한 번 생성해 고정했다 (context-notes.md 참고).
        public static readonly IReadOnlyList<SpawnPoint> All = new List<SpawnPoint>
        {
            new SpawnPoint("B01", ZoneId.Beginner, MonsterType.Imp, 34.8f, 103.5f),
            new SpawnPoint("B02", ZoneId.Beginner, MonsterType.Imp, 96.7f, 94.2f),
            new SpawnPoint("B03", ZoneId.Beginner, MonsterType.Imp, 173.0f, 99.0f),
            new SpawnPoint("B04", ZoneId.Beginner, MonsterType.Imp, 224.9f, 104.5f),
            new SpawnPoint("B05", ZoneId.Beginner, MonsterType.Imp, 294.2f, 100.0f),
            new SpawnPoint("B06", ZoneId.Beginner, MonsterType.Imp, 365.5f, 95.3f),
            new SpawnPoint("B07", ZoneId.Beginner, MonsterType.Imp, 48.1f, 135.6f),
            new SpawnPoint("B08", ZoneId.Beginner, MonsterType.Imp, 100.0f, 128.3f),
            new SpawnPoint("B09", ZoneId.Beginner, MonsterType.Imp, 164.1f, 132.9f),
            new SpawnPoint("B10", ZoneId.Beginner, MonsterType.Imp, 241.7f, 134.1f),
            new SpawnPoint("B11", ZoneId.Beginner, MonsterType.Imp, 291.9f, 130.7f),
            new SpawnPoint("B12", ZoneId.Beginner, MonsterType.Imp, 362.6f, 123.1f),
            new SpawnPoint("B13", ZoneId.Beginner, MonsterType.Imp, 31.5f, 162.7f),
            new SpawnPoint("B14", ZoneId.Beginner, MonsterType.Imp, 101.8f, 161.5f),
            new SpawnPoint("B15", ZoneId.Beginner, MonsterType.Imp, 177.8f, 161.4f),
            new SpawnPoint("B16", ZoneId.Beginner, MonsterType.Imp, 222.4f, 165.6f),
            new SpawnPoint("B17", ZoneId.Beginner, MonsterType.Imp, 299.8f, 161.6f),
            new SpawnPoint("B18", ZoneId.Beginner, MonsterType.Imp, 367.8f, 166.1f),
            new SpawnPoint("B19", ZoneId.Beginner, MonsterType.Imp, 31.9f, 191.2f),
            new SpawnPoint("B20", ZoneId.Beginner, MonsterType.Imp, 102.7f, 190.6f),
            new SpawnPoint("B21", ZoneId.Beginner, MonsterType.Imp, 171.8f, 191.3f),
            new SpawnPoint("B22", ZoneId.Beginner, MonsterType.Imp, 241.3f, 184.4f),
            new SpawnPoint("B23", ZoneId.Beginner, MonsterType.Imp, 292.3f, 185.0f),
            new SpawnPoint("B24", ZoneId.Beginner, MonsterType.Imp, 353.2f, 192.9f),
            new SpawnPoint("I01", ZoneId.Intermediate, MonsterType.VandalImp, 46.6f, 218.1f),
            new SpawnPoint("I02", ZoneId.Intermediate, MonsterType.VandalImp, 136.2f, 223.1f),
            new SpawnPoint("I03", ZoneId.Intermediate, MonsterType.VandalImp, 204.2f, 221.5f),
            new SpawnPoint("I04", ZoneId.Intermediate, MonsterType.VandalImp, 274.7f, 221.3f),
            new SpawnPoint("I05", ZoneId.Intermediate, MonsterType.VandalImp, 352.5f, 220.3f),
            new SpawnPoint("I06", ZoneId.Intermediate, MonsterType.VandalImp, 43.6f, 245.7f),
            new SpawnPoint("I07", ZoneId.Intermediate, MonsterType.VandalImp, 114.6f, 248.3f),
            new SpawnPoint("I08", ZoneId.Intermediate, MonsterType.VandalImp, 197.5f, 249.2f),
            new SpawnPoint("I09", ZoneId.Intermediate, MonsterType.VandalImp, 282.7f, 244.7f),
            new SpawnPoint("I10", ZoneId.Intermediate, MonsterType.VandalImp, 358.9f, 251.3f),
            new SpawnPoint("I11", ZoneId.Intermediate, MonsterType.VandalImp, 54.0f, 270.6f),
            new SpawnPoint("I12", ZoneId.Intermediate, MonsterType.VandalImp, 115.0f, 278.1f),
            new SpawnPoint("I13", ZoneId.Intermediate, MonsterType.VandalImp, 195.1f, 274.0f),
            new SpawnPoint("I14", ZoneId.Intermediate, MonsterType.VandalImp, 279.8f, 276.5f),
            new SpawnPoint("I15", ZoneId.Intermediate, MonsterType.VandalImp, 340.5f, 281.8f),
            new SpawnPoint("I16", ZoneId.Intermediate, MonsterType.VandalImp, 50.2f, 307.8f),
            new SpawnPoint("I17", ZoneId.Intermediate, MonsterType.VandalImp, 125.2f, 304.8f),
            new SpawnPoint("I18", ZoneId.Intermediate, MonsterType.VandalImp, 211.8f, 298.8f),
            new SpawnPoint("I19", ZoneId.Intermediate, MonsterType.VandalImp, 276.1f, 300.4f),
            new SpawnPoint("I20", ZoneId.Intermediate, MonsterType.VandalImp, 351.0f, 306.1f),
            new SpawnPoint("X01", ZoneId.Boss, MonsterType.ImpBoss, 200.0f, 372.0f),
            new SpawnPoint("X02", ZoneId.Boss, MonsterType.VandalImp, 170.0f, 352.0f),
            new SpawnPoint("X03", ZoneId.Boss, MonsterType.VandalImp, 230.0f, 352.0f),
            new SpawnPoint("X04", ZoneId.Boss, MonsterType.VandalImp, 155.0f, 382.0f),
            new SpawnPoint("X05", ZoneId.Boss, MonsterType.VandalImp, 245.0f, 382.0f),
        };

        private static Dictionary<string, SpawnPoint> _byId;

        public static bool TryGet(string id, out SpawnPoint spawn)
        {
            if (_byId == null)
            {
                var map = new Dictionary<string, SpawnPoint>();
                foreach (var s in All) map[s.Id] = s;
                _byId = map;
            }
            spawn = null;
            return id != null && _byId.TryGetValue(id, out spawn);
        }
    }
}
