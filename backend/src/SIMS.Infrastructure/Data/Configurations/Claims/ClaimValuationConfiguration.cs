using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations.Claims;

public class ClaimValuationConfiguration : IEntityTypeConfiguration<ClaimValuation>
{
    public void Configure(EntityTypeBuilder<ClaimValuation> builder)
    {
        builder.ToTable("claim_valuations");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Paid).HasPrecision(18, 2);
        builder.Property(v => v.Reserved).HasPrecision(18, 2);
        builder.Property(v => v.Expense).HasPrecision(18, 2);
        builder.Property(v => v.Recovery).HasPrecision(18, 2);
        builder.Property(v => v.Incurred).HasPrecision(18, 2);

        // One snapshot per claim per valuation date; re-imports upsert
        builder.HasIndex(v => new { v.ClaimId, v.ValuationDate }).IsUnique();
        builder.HasIndex(v => v.ValuationDate);

        builder.HasOne(v => v.Claim)
            .WithMany(c => c.Valuations)
            .HasForeignKey(v => v.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
