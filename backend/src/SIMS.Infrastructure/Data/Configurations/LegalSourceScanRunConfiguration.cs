using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegalSourceScanRunConfiguration : IEntityTypeConfiguration<LegalSourceScanRun>
{
    public void Configure(EntityTypeBuilder<LegalSourceScanRun> builder)
    {
        builder.ToTable("legal_source_scan_runs");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.SourceName).IsRequired().HasMaxLength(120);
        builder.Property(r => r.SourceType).IsRequired().HasMaxLength(80);
        builder.Property(r => r.Status).IsRequired().HasMaxLength(40);
        builder.Property(r => r.ErrorMessage).HasMaxLength(2000);
        builder.Property(r => r.StartedByName).HasMaxLength(200);

        builder.HasOne(r => r.StartedBy).WithMany()
            .HasForeignKey(r => r.StartedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.SourceName, r.StartedAt });
        builder.HasIndex(r => r.Status);
    }
}
