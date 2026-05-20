using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingAppetiteResultConfiguration : IEntityTypeConfiguration<UnderwritingAppetiteResult>
{
    public void Configure(EntityTypeBuilder<UnderwritingAppetiteResult> builder)
    {
        builder.Property(r => r.RuleCode).IsRequired().HasMaxLength(100);
        builder.Property(r => r.RuleName).IsRequired().HasMaxLength(160);
        builder.Property(r => r.Explanation).IsRequired().HasMaxLength(500);

        builder.HasIndex(r => new { r.SubmissionId, r.QuoteId, r.RuleCode });

        builder.HasOne(r => r.Submission)
            .WithMany()
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Quote)
            .WithMany()
            .HasForeignKey(r => r.QuoteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.EvaluatedBy)
            .WithMany()
            .HasForeignKey(r => r.EvaluatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
