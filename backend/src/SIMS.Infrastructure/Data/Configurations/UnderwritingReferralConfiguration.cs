using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingReferralConfiguration : IEntityTypeConfiguration<UnderwritingReferral>
{
    public void Configure(EntityTypeBuilder<UnderwritingReferral> builder)
    {
        builder.Property(r => r.ReferralType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
        builder.Property(r => r.DecisionNotes).HasMaxLength(1000);

        builder.HasIndex(r => new { r.SubmissionId, r.QuoteId, r.ReferralType });

        builder.HasOne(r => r.Submission)
            .WithMany()
            .HasForeignKey(r => r.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Quote)
            .WithMany()
            .HasForeignKey(r => r.QuoteId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.RequestedBy)
            .WithMany()
            .HasForeignKey(r => r.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.DecisionBy)
            .WithMany()
            .HasForeignKey(r => r.DecisionById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
