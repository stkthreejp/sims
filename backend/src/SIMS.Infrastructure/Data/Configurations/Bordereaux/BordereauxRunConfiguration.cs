using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Bordereaux;

namespace SIMS.Infrastructure.Data.Configurations.Bordereaux;

public class BordereauxRunConfiguration : IEntityTypeConfiguration<BordereauxRun>
{
    public void Configure(EntityTypeBuilder<BordereauxRun> builder)
    {
        builder.ToTable("bordereaux_runs");

        builder.Property(x => x.LondonBordereauxBlobPath).HasMaxLength(500);
        builder.Property(x => x.LondonBordereauxFileName).HasMaxLength(255);
        builder.Property(x => x.LondonBordereauxContentType).HasMaxLength(150);
        builder.Property(x => x.AccountCurrentBlobPath).HasMaxLength(500);
        builder.Property(x => x.AccountCurrentFileName).HasMaxLength(255);
        builder.Property(x => x.AccountCurrentContentType).HasMaxLength(150);
        builder.Property(x => x.DetailRowCountsJson).HasColumnType("jsonb");
        builder.Property(x => x.ValidationSummaryJson).HasColumnType("jsonb");
        builder.Property(x => x.ReconciliationSummaryJson).HasColumnType("jsonb");

        builder.HasIndex(x => new { x.BordereauxProfileId, x.PeriodStart, x.PeriodEnd });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ReconciliationStatus);

        builder.HasOne(x => x.Profile)
            .WithMany(p => p.Runs)
            .HasForeignKey(x => x.BordereauxProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.GeneratedBy)
            .WithMany()
            .HasForeignKey(x => x.GeneratedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
