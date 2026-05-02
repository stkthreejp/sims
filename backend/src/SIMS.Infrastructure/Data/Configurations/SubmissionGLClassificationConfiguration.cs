using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionGLClassificationConfiguration : IEntityTypeConfiguration<SubmissionGLClassification>
{
    public void Configure(EntityTypeBuilder<SubmissionGLClassification> builder)
    {
        builder.ToTable("submission_gl_classifications");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ClassCode).HasMaxLength(20);
        builder.Property(c => c.Description).HasMaxLength(500);
        builder.Property(c => c.PremiumBasis).HasMaxLength(100);
        builder.Property(c => c.Exposure).HasPrecision(18, 2);

        builder.HasOne(c => c.Submission).WithMany(s => s.GLClassifications)
            .HasForeignKey(c => c.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}
