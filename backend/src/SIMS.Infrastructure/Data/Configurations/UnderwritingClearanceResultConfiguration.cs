using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingClearanceResultConfiguration : IEntityTypeConfiguration<UnderwritingClearanceResult>
{
    public void Configure(EntityTypeBuilder<UnderwritingClearanceResult> builder)
    {
        builder.Property(r => r.MatchedRecordLabel).HasMaxLength(120);
        builder.Property(r => r.Explanation).HasMaxLength(500);
        builder.Property(r => r.SnapshotJson).HasColumnType("jsonb");

        builder.HasIndex(r => new { r.SubmissionId, r.CheckType });
        builder.HasOne(r => r.Submission)
            .WithMany()
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
