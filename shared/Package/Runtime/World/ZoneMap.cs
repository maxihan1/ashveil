// 월드 좌표로 구역을 판정하는 정적 지도 (마을·초급·중급·보스)
namespace Ashveil.Shared
{
    public struct ZoneBounds
    {
        public readonly float MinX, MinZ, MaxX, MaxZ;

        public ZoneBounds(float minX, float minZ, float maxX, float maxZ)
        {
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }

        /// <summary>최소값 포함, 최대값 제외.</summary>
        public bool Contains(float x, float z) => x >= MinX && x < MaxX && z >= MinZ && z < MaxZ;
    }

    public static class ZoneMap
    {
        public const float WorldSize = 400f;
        public const float BeginnerMaxZ = 200f;

        /// <summary>마을 부활 지점.</summary>
        public const float RespawnX = 200f;
        public const float RespawnZ = 30f;

        // 마을은 남쪽 가운데, 보스 지역은 북쪽 끝. 월드 가장자리(좌표 0, 400)도 안쪽으로 치도록 여유를 둔다.
        public static readonly ZoneBounds Town = new ZoneBounds(150f, -1f, 250f, 80f);
        public static readonly ZoneBounds Boss = new ZoneBounds(130f, 320f, 270f, WorldSize + 1f);

        public static ZoneId GetZone(float x, float z)
        {
            if (Town.Contains(x, z)) return ZoneId.Town;
            if (Boss.Contains(x, z)) return ZoneId.Boss;
            return z < BeginnerMaxZ ? ZoneId.Beginner : ZoneId.Intermediate;
        }

        public static bool IsSafeZone(ZoneId zone) => zone == ZoneId.Town;

        /// <summary>구역을 감싸는 사각형. 초급·중급은 마을·보스 지역을 포함하므로 표본을 뽑을 땐 GetZone 으로 다시 확인한다.</summary>
        public static ZoneBounds BoundsOf(ZoneId zone)
        {
            switch (zone)
            {
                case ZoneId.Town: return new ZoneBounds(Town.MinX, 0f, Town.MaxX, Town.MaxZ);
                case ZoneId.Boss: return new ZoneBounds(Boss.MinX, Boss.MinZ, Boss.MaxX, WorldSize);
                case ZoneId.Beginner: return new ZoneBounds(0f, 0f, WorldSize, BeginnerMaxZ);
                default: return new ZoneBounds(0f, BeginnerMaxZ, WorldSize, WorldSize);
            }
        }

        /// <summary>구역 안쪽의 대표 지점(텔레포트, 토스트 테스트용).</summary>
        public static (float X, float Z) CenterOf(ZoneId zone)
        {
            switch (zone)
            {
                case ZoneId.Town: return (200f, 40f);
                case ZoneId.Beginner: return (100f, 140f);
                case ZoneId.Intermediate: return (200f, 260f);
                default: return (200f, 345f);
            }
        }

        public static string DisplayName(ZoneId zone)
        {
            switch (zone)
            {
                case ZoneId.Town: return "하늘빛 마을";
                case ZoneId.Beginner: return "초록 들판";
                case ZoneId.Intermediate: return "잿빛 숲";
                default: return "불꽃 군주의 폐허";
            }
        }

        public static float ClampToWorld(float v) => v < 0f ? 0f : (v > WorldSize ? WorldSize : v);
    }
}
