using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class QuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> builder)
    {
        builder.ToTable("quotes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.QuoteNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(q => q.QuoteNumber).IsUnique();
        builder.Property(q => q.PolicyNumber).HasMaxLength(50);
        builder.HasIndex(q => q.PolicyNumber).IsUnique();

        builder.Property(q => q.PremiumAmount).HasPrecision(18, 2);
        builder.Property(q => q.TaxesAndFees).HasPrecision(18, 2);
        builder.Property(q => q.TotalPremium).HasPrecision(18, 2);
        builder.Property(q => q.CommissionRate).HasPrecision(5, 4);
        builder.Property(q => q.CommissionAmount).HasPrecision(18, 2);
        builder.Property(q => q.Deductible).HasPrecision(18, 2);
        builder.Property(q => q.Limit).HasPrecision(18, 2);
        builder.Property(q => q.UninsuredMotoristLimit).HasPrecision(18, 2);
        builder.Property(q => q.MedicalPaymentsLimit).HasPrecision(18, 2);

        builder.HasOne(q => q.Submission).WithMany(s => s.Quotes)
            .HasForeignKey(q => q.SubmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.Carrier).WithMany(c => c.Quotes)
            .HasForeignKey(q => q.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(q => q.CreatedBy).WithMany()
            .HasForeignKey(q => q.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
