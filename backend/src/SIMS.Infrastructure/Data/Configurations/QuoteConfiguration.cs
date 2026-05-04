using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(q => q.QuoteNumber).IsUnique();
        builder.Property(q => q.PolicyNumber).HasMaxLength(50);

        builder.Property(q => q.PremiumAmount).HasPrecision(18, 2);
        builder.Property(q => q.TaxesAndFees).HasPrecision(18, 2);
        builder.Property(q => q.TotalPremium).HasPrecision(18, 2);
        builder.Property(q => q.CarrierCommissionRate).HasPrecision(8, 6);
        builder.Property(q => q.SMMRetentionRate).HasPrecision(8, 6);
        builder.Property(q => q.AgentCommissionRate).HasPrecision(8, 6);
        builder.Property(q => q.CommissionOverrideCarrierRate).HasPrecision(8, 6);
        builder.Property(q => q.CommissionOverrideSMMRate).HasPrecision(8, 6);
        builder.Property(q => q.CommissionOverrideAgentRate).HasPrecision(8, 6);
        builder.Property(q => q.Deductible).HasPrecision(18, 2);
        builder.Property(q => q.Limit).HasPrecision(18, 2);
        builder.Property(q => q.UninsuredMotoristLimit).HasPrecision(18, 2);
        builder.Property(q => q.MedicalPaymentsLimit).HasPrecision(18, 2);

        builder.Property(q => q.CompanyId).HasColumnName("company_id");
        builder.Property(q => q.ProducerId).HasColumnName("producer_id");
        builder.Property(q => q.IsFilingState).HasColumnName("is_filing_state");

        // Computed helper properties — not mapped to columns
        builder.Ignore(q => q.EffectiveCarrierRate);
        builder.Ignore(q => q.EffectiveSMMRate);
        builder.Ignore(q => q.EffectiveAgentRate);
        builder.Ignore(q => q.HasCommissionOverride);

        builder.HasOne(q => q.Submission).WithMany(s => s.Quotes)
            .HasForeignKey(q => q.SubmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Carrier).WithMany(c => c.Quotes)
            .HasForeignKey(q => q.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.CreatedBy).WithMany()
            .HasForeignKey(q => q.CreatedById).OnDelete(DeleteBehavior.Restrict);

        // PolicyNumber unique index allows nulls
        builder.HasIndex(q => q.PolicyNumber).IsUnique().HasFilter("policy_number IS NOT NULL");
    }
}
