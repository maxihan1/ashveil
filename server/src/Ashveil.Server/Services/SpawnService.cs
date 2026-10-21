// 몬스터 스폰 표: 기본값(SpawnTable) 시드·초기화와 DTO 변환
using Ashveil.Server.Data;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Services;

public static class SpawnService
{
    public static async Task SeedIfEmptyAsync(GameDbContext db)
    {
        if (await db.MonsterSpawns.AnyAsync()) return;
        db.MonsterSpawns.AddRange(Defaults());
        await db.SaveChangesAsync();
    }

    /// <summary>스폰 표를 기본값으로 되돌린다. 호출한 쪽이 저장한다.</summary>
    public static async Task ResetAsync(GameDbContext db)
    {
        await db.MonsterSpawns.ExecuteDeleteAsync();
        db.MonsterSpawns.AddRange(Defaults());
    }

    public static SpawnPointDto ToDto(MonsterSpawn s) => new() { Id = s.Id, Zone = s.Zone, Monster = s.Monster, X = s.X, Z = s.Z };

    private static IEnumerable<MonsterSpawn> Defaults() =>
        SpawnTable.All.Select(p => new MonsterSpawn { Id = p.Id, Zone = p.Zone, Monster = p.Monster, X = p.X, Z = p.Z });
}
