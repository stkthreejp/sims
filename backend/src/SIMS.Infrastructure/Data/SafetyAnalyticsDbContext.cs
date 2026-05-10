using Microsoft.EntityFrameworkCore;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.FmcsaAnalytics;
using SIMS.Infrastructure.Data.Configurations.FmcsaAnalytics;

namespace SIMS.Infrastructure.Data;

public class SafetyAnalyticsDbContext : DbContext
{
    public SafetyAnalyticsDbContext(DbContextOptions<SafetyAnalyticsDbContext> options) : base(options) { }

    public DbSet<FmcsaAnalyticsImportBatch> FmcsaAnalyticsImportBatches => Set<FmcsaAnalyticsImportBatch>();
    public DbSet<FmcsaCarrierPeerSnapshot> FmcsaCarrierPeerSnapshots => Set<FmcsaCarrierPeerSnapshot>();
    public DbSet<FmcsaBasicPeerMeasure> FmcsaBasicPeerMeasures => Set<FmcsaBasicPeerMeasure>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfiguration(new FmcsaAnalyticsImportBatchConfiguration());
        builder.ApplyConfiguration(new FmcsaCarrierPeerSnapshotConfiguration());
        builder.ApplyConfiguration(new FmcsaBasicPeerMeasureConfiguration());

        builder.Entity<FmcsaAnalyticsImportBatch>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaCarrierPeerSnapshot>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<FmcsaBasicPeerMeasure>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
    }
}
