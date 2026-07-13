using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class IntakeJobConfiguration : IEntityTypeConfiguration<IntakeJob>
{
    public void Configure(EntityTypeBuilder<IntakeJob> builder)
    {
        builder.ToTable("intake_jobs");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Stage).HasMaxLength(50);
        builder.Property(j => j.ErrorMessage).HasMaxLength(2000);

        // Worker drains the oldest queued job first.
        builder.HasIndex(j => new { j.Status, j.CreatedAt });
        // "Latest job for this submission" lookup (status endpoint).
        builder.HasIndex(j => j.SubmissionId);

        builder.HasOne(j => j.Submission).WithMany()
            .HasForeignKey(j => j.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
