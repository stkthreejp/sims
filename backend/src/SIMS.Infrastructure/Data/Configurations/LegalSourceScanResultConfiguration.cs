using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegalSourceScanResultConfiguration : IEntityTypeConfiguration<LegalSourceScanResult>
{
    public void Configure(EntityTypeBuilder<LegalSourceScanResult> builder)
    {
        builder.ToTable("legal_source_scan_results");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.State).IsRequired().HasMaxLength(80);
        builder.Property(r => r.Category).IsRequired().HasMaxLength(120);
        builder.Property(r => r.Topic).IsRequired().HasMaxLength(160);
        builder.Property(r => r.MatchStatus).IsRequired().HasMaxLength(40);
        builder.Property(r => r.SourceUrl).HasMaxLength(1000);
        builder.Property(r => r.SourceCitation).HasMaxLength(300);
        builder.Property(r => r.SourceText).IsRequired();
        builder.Property(r => r.ReviewStatus).IsRequired().HasMaxLength(40);
        builder.Property(r => r.ReviewedByName).HasMaxLength(200);
        builder.Property(r => r.ConfidenceScore).HasPrecision(5, 4);

        builder.HasOne(r => r.ScanRun).WithMany(r => r.Results)
            .HasForeignKey(r => r.ScanRunId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.RequirementSection).WithMany()
            .HasForeignKey(r => r.RequirementSectionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ReviewedBy).WithMany()
            .HasForeignKey(r => r.ReviewedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(r => new { r.State, r.Category, r.Topic });
        builder.HasIndex(r => r.MatchStatus);
        builder.HasIndex(r => r.ReviewStatus);
    }
}
