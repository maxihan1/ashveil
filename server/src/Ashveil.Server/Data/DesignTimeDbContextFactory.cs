// dotnet ef 마이그레이션 생성 시 Program 실행 없이 DbContext 를 만드는 팩토리
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ashveil.Server.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Database=ashveil_design")
            .UseSnakeCaseNamingConvention()
            .Options);
}
