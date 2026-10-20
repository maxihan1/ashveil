// 테스트마다 전용 PostgreSQL 데이터베이스를 만들고 실제 서버 파이프라인을 띄우는 픽스처
using System.Collections.Concurrent;
using Ashveil.Shared;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Ashveil.Server.Tests.Infrastructure;

/// <summary>QA 환경(GM API 사용 가능) 서버.</summary>
public class ServerFixture : IAsyncLifetime
{
    protected virtual string EnvironmentName => "Qa";

    public ControlledRandom Random { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    private string _database = "";

    public static string AdminConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__TestAdmin")
        ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    public Task InitializeAsync()
    {
        _database = "ashveil_test_" + Guid.NewGuid().ToString("N")[..12];
        var connection = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = _database }.ConnectionString;

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment(EnvironmentName);
            b.UseSetting("ConnectionStrings:Game", connection);
            b.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789abcdef");
            b.ConfigureServices(s =>
            {
                s.RemoveAll<IRandomSource>();
                s.AddSingleton<IRandomSource>(Random);
            });
        });
        _ = Factory.Server; // 시작 시 마이그레이션과 봇 시드가 돈다
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    public GameClient NewClient() => new(Factory.CreateClient());

    /// <summary>새 계정 + 캐릭터를 만들고 인증·캐릭터 헤더가 붙은 클라이언트를 돌려준다.</summary>
    public async Task<(GameClient Client, Shared.Dto.CharacterDto Character)> NewPlayerAsync(ClassType classType = ClassType.Warrior)
    {
        var client = NewClient();
        await client.SignupAndLoginAsync();
        var character = await client.CreateCharacterAsync(classType);
        client.UseCharacter(character.Id);
        return (client, character);
    }
}

/// <summary>운영 환경(GM 비활성) 서버.</summary>
public sealed class ProductionServerFixture : ServerFixture
{
    protected override string EnvironmentName => "Production";
}

/// <summary>미리 넣은 값을 차례로 쓰고, 비면 기본값을 돌려주는 난수.</summary>
public sealed class ControlledRandom : IRandomSource
{
    private readonly ConcurrentQueue<double> _queue = new();

    public double Default { get; set; } = 0.5;

    public void Enqueue(params double[] values)
    {
        foreach (var v in values) _queue.Enqueue(v);
    }

    public void Clear() => _queue.Clear();

    public double NextDouble() => _queue.TryDequeue(out var v) ? v : Default;

    public int Next(int minInclusive, int maxExclusive)
    {
        var value = minInclusive + (int)Math.Floor(NextDouble() * (maxExclusive - minInclusive));
        return Math.Clamp(value, minInclusive, maxExclusive - 1);
    }
}
