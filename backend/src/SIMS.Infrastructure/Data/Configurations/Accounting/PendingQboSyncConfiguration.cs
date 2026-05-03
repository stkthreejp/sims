using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class PendingQboSyncConfiguration : IEntityTypeConfiguration<PendingQboSync>
{
    public void Configure(EntityTypeBuilder<PendingQboSync> b)
    {
        b.ToTable("pending_qbo_syncs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.Status).IsRequired().HasMaxLength(20);
        b.Property(x => x.LastError).HasMaxLength(2000);
        b.HasOne(x => x.Rollup)
            .WithMany()
            .HasForeignKey(x => x.RollupId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.Status).HasDatabaseName("ix_pending_qbo_syncs_status");
        b.HasIndex(x => x.NextRetryAt).HasDatabaseName("ix_pending_qbo_syncs_next_retry");
    }
}
