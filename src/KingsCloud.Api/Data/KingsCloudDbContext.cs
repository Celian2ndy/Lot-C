using Microsoft.EntityFrameworkCore;

namespace Kings.Cloud.Api.Data;

public sealed class KingsCloudDbContext : DbContext
{
    public KingsCloudDbContext(DbContextOptions<KingsCloudDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<LeaderboardEntry> LeaderboardEntries => Set<LeaderboardEntry>();
    public DbSet<Pack> Packs => Set<Pack>();
    public DbSet<TelemetryResult> TelemetryResults => Set<TelemetryResult>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountHash).IsUnique();
            e.Property(x => x.AccountHash).IsRequired();
            e.Property(x => x.Display).IsRequired();
            e.HasOne(x => x.License).WithOne(l => l.Account!).HasForeignKey<License>(l => l.AccountId);
        });

        b.Entity<License>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.LicenseKeyHash).IsUnique();
            e.Property(x => x.Plan).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
        });

        b.Entity<Session>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TokenHash).IsUnique();
        });

        b.Entity<LeaderboardEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AccountId).IsUnique(); // une entrée par compte
            e.HasIndex(x => x.RecomputedScore);
        });

        b.Entity<Pack>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PayloadJson).HasColumnType("jsonb");
        });

        b.Entity<TelemetryResult>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ConfigHash);
        });
    }
}
