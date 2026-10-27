// PostgreSQL 스키마 정의(인덱스·제약)와 행 잠금 조회 헬퍼
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Data;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<GroundItem> GroundItems => Set<GroundItem>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<EnhanceHistory> EnhanceHistories => Set<EnhanceHistory>();
    public DbSet<SpawnCooldown> SpawnCooldowns => Set<SpawnCooldown>();
    public DbSet<PkRecord> PkRecords => Set<PkRecord>();
    public DbSet<AlignmentHistory> AlignmentHistories => Set<AlignmentHistory>();
    public DbSet<MonsterSpawn> MonsterSpawns => Set<MonsterSpawn>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // enum은 DB에서도 읽기 쉽게 문자열로 저장한다.
        builder.Properties<Enum>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Username).HasMaxLength(32);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.AccountId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Character>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(32);
            e.Property(x => x.NameKey).HasMaxLength(32);
            e.HasIndex(x => x.NameKey).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.AccountId);
            e.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("ck_characters_gold_non_negative", "gold >= 0"));
        });

        b.Entity<Bot>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.HasIndex(x => x.Zone);
        });

        b.Entity<Item>(e =>
        {
            e.HasIndex(x => new { x.OwnerKind, x.OwnerId });
            e.ToTable(t => t.HasCheckConstraint("ck_items_quantity_positive", "quantity > 0"));
        });

        b.Entity<GroundItem>(e =>
        {
            e.HasKey(x => x.ItemId);
            e.HasOne(x => x.Item).WithOne().HasForeignKey<GroundItem>(x => x.ItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.Zone, x.ExpiresAt });
        });

        b.Entity<LedgerEntry>(e =>
        {
            e.HasIndex(x => new { x.CharacterId, x.Id });
            e.HasOne<Character>().WithMany().HasForeignKey(x => x.CharacterId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EnhanceHistory>(e => e.HasIndex(x => x.CharacterId));

        b.Entity<SpawnCooldown>(e => e.HasKey(x => new { x.CharacterId, x.SpawnId }));

        b.Entity<PkRecord>(e =>
        {
            e.HasIndex(x => new { x.KillerKind, x.KillerId });
            e.HasIndex(x => new { x.VictimKind, x.VictimId });
        });

        b.Entity<AlignmentHistory>(e => e.HasIndex(x => new { x.EntityKind, x.EntityId }));

        b.Entity<MonsterSpawn>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(16);
        });
    }

    // 행 잠금(SELECT ... FOR UPDATE). 잠금 순서는 캐릭터 → 봇 → 바닥 아이템으로 고정해 교착을 막는다.
    // 같은 엔티티를 먼저 일반 조회하면 추적 중인 오래된 값이 반환되므로, 트랜잭션의 첫 조회로 호출한다.

    public Task<Character?> LockCharacterAsync(long id) =>
        Characters.FromSql($"SELECT * FROM characters WHERE id = {id} AND deleted_at IS NULL FOR UPDATE")
            .SingleOrDefaultAsync();

    public Task<Bot?> LockBotAsync(long id) =>
        Bots.FromSql($"SELECT * FROM bots WHERE id = {id} FOR UPDATE").SingleOrDefaultAsync();

    public Task<GroundItem?> LockGroundItemAsync(long itemId) =>
        GroundItems.FromSql($"SELECT * FROM ground_items WHERE item_id = {itemId} FOR UPDATE")
            .Include(g => g.Item)
            .SingleOrDefaultAsync();
}
