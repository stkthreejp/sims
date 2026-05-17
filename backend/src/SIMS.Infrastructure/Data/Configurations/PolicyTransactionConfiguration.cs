using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyTransactionConfiguration : IEntityTypeConfiguration<PolicyTransaction>
{
    public void Configure(EntityTypeBuilder<PolicyTransaction> builder)
    {
        builder.ToTable("policy_transactions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TransactionNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(t => t.TransactionNumber).IsUnique();
        builder.Property(t => t.ReasonCode).HasMaxLength(100);
        builder.Property(t => t.ReasonText).HasMaxLength(1000);
        builder.Property(t => t.PremiumChange).HasPrecision(18, 2);
        builder.Property(t => t.NewTotalPremium).HasPrecision(18, 2);
        builder.Property(t => t.PremiumBefore).HasPrecision(18, 2);
        builder.Property(t => t.PremiumAfter).HasPrecision(18, 2);
        builder.Property(t => t.TaxesAndFeesDelta).HasPrecision(18, 2);
        builder.Property(t => t.CommissionDelta).HasPrecision(18, 2);
        builder.Property(t => t.BillingModeSnapshot).HasMaxLength(100);
        builder.Property(t => t.ExternalReference).HasMaxLength(100);
        builder.Property(t => t.CancellationReason).HasMaxLength(500);
        builder.Property(t => t.CancellationMethod).HasMaxLength(50);
        builder.Property(t => t.CancellationComplianceChecklistJson).HasColumnType("text");
        builder.Property(t => t.CancellationLegalRequirementSnapshotJson).HasColumnType("text");
        builder.Property(t => t.EndorsementDescription).HasMaxLength(2000);
        builder.Property(t => t.Notes).HasMaxLength(2000);

        builder.HasOne(t => t.Policy).WithMany(p => p.Transactions)
            .HasForeignKey(t => t.PolicyId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.ProcessedBy).WithMany()
            .HasForeignKey(t => t.ProcessedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.PriorPolicy).WithMany()
            .HasForeignKey(t => t.PriorPolicyId).OnDelete(DeleteBehavior.SetNull);
    }
}
