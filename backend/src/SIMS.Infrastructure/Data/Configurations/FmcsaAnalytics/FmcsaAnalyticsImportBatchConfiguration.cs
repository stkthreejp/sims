using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Infrastructure.Data.Configurations.Fmcsa;
using SIMS.Domain.Entities.FmcsaAnalytics;

namespace SIMS.Infrastructure.Data.Configurations.FmcsaAnalytics;

public class FmcsaAnalyticsImportBatchConfiguration : IEntityTypeConfiguration<FmcsaAnalyticsImportBatch>
{
    public void Configure(EntityTypeBuilder<FmcsaAnalyticsImportBatch> builder)
    {
        builder.ToTable("fmcsa_analytics_import_batches");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.SnapshotMonth).HasColumnName("snapshot_month").HasMaxLength(7).IsRequired();
        builder.Property(x => x.SourceName).HasColumnName("source_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.StartedAt).HasColumnName("started_at");
        builder.Property(x => x.CompletedAt).HasColumnName("completed_at");
        builder.Property(x => x.RowsImported).HasColumnName("rows_imported");
        builder.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.HasIndex(x => new { x.SnapshotMonth, x.SourceName });
    }
}
