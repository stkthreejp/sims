using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations.Claims;

public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("claims");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.PolicyNumber).HasMaxLength(100);
        builder.Property(c => c.InsuredName).HasMaxLength(500);
        builder.Property(c => c.ClaimNumber).HasMaxLength(100);
        builder.Property(c => c.CarrierClaimNumber).HasMaxLength(100);
        builder.Property(c => c.SourcePolicyReference).HasMaxLength(200);
        builder.Property(c => c.Account).HasMaxLength(200);
        builder.Property(c => c.CarrierName).HasMaxLength(500);
        builder.Property(c => c.CoverageType).HasMaxLength(50);
        builder.Property(c => c.ClaimTypeDesc).HasMaxLength(200);
        builder.Property(c => c.LossCause).HasMaxLength(500);
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.RiskState).HasMaxLength(2);
        builder.Property(c => c.AccidentState).HasMaxLength(2);
        builder.Property(c => c.ClaimantName).HasMaxLength(500);
        builder.Property(c => c.AdjusterName).HasMaxLength(500);
        builder.Property(c => c.TpaName).HasMaxLength(500);
        builder.Property(c => c.TpaClaimNumber).HasMaxLength(100);
        builder.Property(c => c.Notes).HasMaxLength(4000);

        builder.Property(c => c.Paid).HasPrecision(18, 2);
        builder.Property(c => c.Reserved).HasPrecision(18, 2);
        builder.Property(c => c.Expense).HasPrecision(18, 2);
        builder.Property(c => c.Recovery).HasPrecision(18, 2);
        builder.Property(c => c.Incurred).HasPrecision(18, 2);

        // Unique on source reference + claim number (handles null PolicyId for unmatched claims)
        builder.HasIndex(c => new { c.SourcePolicyReference, c.ClaimNumber }).IsUnique();
        builder.HasIndex(c => c.PolicyId);
        builder.HasIndex(c => c.InsuredId);
        builder.HasIndex(c => c.DateOfLoss);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.Account);

        builder.HasOne(c => c.Policy)
            .WithMany()
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ImportBatch)
            .WithMany(b => b.Claims)
            .HasForeignKey(c => c.ImportBatchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
