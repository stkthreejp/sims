using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeePremiumBracketConfiguration : IEntityTypeConfiguration<FeePremiumBracket>
{
    public void Configure(EntityTypeBuilder<FeePremiumBracket> b)
    {
        b.ToTable("fee_premium_brackets");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.TierFrom).HasColumnType("numeric(19,4)");
        b.Property(x => x.TierTo).HasColumnType("numeric(19,4)");
        b.Property(x => x.PercentRate).HasColumnType("numeric(9,6)");

        b.HasOne(x => x.FeeRuleVersion)
            .WithMany(x => x.PremiumBrackets)
            .HasForeignKey(x => x.FeeRuleVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.FeeRuleVersionId, x.TierFrom }).IsUnique();
    }
}
