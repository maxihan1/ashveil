// 구역 판정, 스폰 표, 몬스터 카탈로그의 정합성 테스트
using Ashveil.Shared;
using Xunit;

namespace Ashveil.Shared.Tests;

public class WorldTests
{
    [Theory]
    [InlineData(200f, 40f, ZoneId.Town)]
    [InlineData(150f, 0f, ZoneId.Town)]
    [InlineData(100f, 40f, ZoneId.Beginner)]   // 마을 옆
    [InlineData(200f, 80f, ZoneId.Beginner)]   // 마을 북쪽 경계 바로 밖
    [InlineData(50f, 199.9f, ZoneId.Beginner)]
    [InlineData(50f, 200f, ZoneId.Intermediate)]
    [InlineData(200f, 372f, ZoneId.Boss)]
    [InlineData(100f, 372f, ZoneId.Intermediate)] // 보스 지역 서쪽 바깥
    public void GetZone_ReturnsZoneByPosition(float x, float z, ZoneId expected)
    {
        Assert.Equal(expected, ZoneMap.GetZone(x, z));
    }

    [Fact]
    public void TownRespawnPoint_IsInsideTown()
    {
        Assert.Equal(ZoneId.Town, ZoneMap.GetZone(ZoneMap.RespawnX, ZoneMap.RespawnZ));
        Assert.True(ZoneMap.IsSafeZone(ZoneId.Town));
        Assert.False(ZoneMap.IsSafeZone(ZoneId.Beginner));
    }

    [Fact]
    public void EverySpawn_LiesInItsDeclaredZone_AndIdsAreUnique()
    {
        var ids = new HashSet<string>();
        foreach (var spawn in SpawnTable.All)
        {
            Assert.True(ids.Add(spawn.Id), $"중복 스폰 ID {spawn.Id}");
            Assert.Equal(spawn.Zone, ZoneMap.GetZone(spawn.X, spawn.Z));
            Assert.InRange(spawn.X, 0f, ZoneMap.WorldSize);
            Assert.InRange(spawn.Z, 0f, ZoneMap.WorldSize);
        }
        Assert.Equal(49, SpawnTable.All.Count);
    }

    [Fact]
    public void SpawnTable_FindById()
    {
        Assert.True(SpawnTable.TryGet("X01", out var boss));
        Assert.Equal(MonsterType.ImpBoss, boss.Monster);
        Assert.False(SpawnTable.TryGet("NOPE", out _));
    }

    [Fact]
    public void MonsterCatalog_HasEveryType_WithSaneRanges()
    {
        foreach (MonsterType type in Enum.GetValues(typeof(MonsterType)))
        {
            var info = MonsterCatalog.Get(type);
            Assert.True(info.GoldMin > 0 && info.GoldMin <= info.GoldMax);
            Assert.True(info.Exp > 0 && info.MaxHp > 0 && info.RespawnSeconds > 0);
        }
    }

    [Fact]
    public void ZoneBounds_ContainOwnCenter()
    {
        foreach (ZoneId zone in Enum.GetValues(typeof(ZoneId)))
        {
            var (x, z) = ZoneMap.CenterOf(zone);
            Assert.Equal(zone, ZoneMap.GetZone(x, z));
        }
    }
}
