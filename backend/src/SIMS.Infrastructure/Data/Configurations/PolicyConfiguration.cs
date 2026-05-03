using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("policies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PolicyNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.Property(p => p.PremiumAmount).HasPrecision(18, 2);
        builder.Property(p => p.TaxesAndFees).HasPrecision(18, 2);
        builder.Property(p => p.TotalPremium).HasPrecision(18, 2);

        builder.HasOne(p => p.Submission).WithMany()
            .HasForeignKey(p => p.SubmissionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.BoundQuote).WithMany()
            .HasForeignKey(p => p.BoundQuoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Carrier).WithMany()
            .HasForeignKey(p => p.CarrierId).OnDelete(DeleteBehavior.Restrict);
    }
}
