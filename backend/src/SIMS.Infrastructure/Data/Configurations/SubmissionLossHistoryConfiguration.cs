using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class SubmissionLossYearConfiguration : IEntityTypeConfiguration<SubmissionLossYear>
{
    public void Configure(EntityTypeBuilder<SubmissionLossYear> builder)
    {
        builder.ToTable("submission_loss_years");
        builder.HasKey(y => y.Id);
        builder.Property(y => y.LineOfBusiness).HasMaxLength(100);
        builder.Property(y => y.CarrierName).HasMaxLength(200);
        builder.Property(y => y.PolicyNumber).HasMaxLength(100);
        builder.Property(y => y.Source).HasMaxLength(100);
        builder.Property(y => y.PremiumAmount).HasPrecision(18, 2);
        builder.Property(y => y.PaidOverride).HasPrecision(18, 2);
        builder.Property(y => y.ReservedOverride).HasPrecision(18, 2);
        builder.Property(y => y.ExpenseOverride).HasPrecision(18, 2);

        builder.HasIndex(y => new { y.SubmissionId, y.PolicyYear, y.LineOfBusiness });

        builder.HasOne(y => y.Submission)
            .WithMany(s => s.LossYears)
            .HasForeignKey(y => y.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SubmissionLossClaimConfiguration : IEntityTypeConfiguration<SubmissionLossClaim>
{
    public void Configure(EntityTypeBuilder<SubmissionLossClaim> builder)
    {
        builder.ToTable("submission_loss_claims");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ClaimNumber).HasMaxLength(100);
        builder.Property(c => c.CoverageType).HasMaxLength(100);
        builder.Property(c => c.Paid).HasPrecision(18, 2);
        builder.Property(c => c.Reserved).HasPrecision(18, 2);
        builder.Property(c => c.Expense).HasPrecision(18, 2);

        builder.HasOne(c => c.LossYear)
            .WithMany(y => y.Claims)
            .HasForeignKey(c => c.SubmissionLossYearId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
