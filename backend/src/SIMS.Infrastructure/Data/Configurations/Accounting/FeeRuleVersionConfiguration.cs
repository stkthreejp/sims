using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeeRuleVersionConfiguration : IEntityTypeConfiguration<FeeRuleVersion>
{
    public void Configure(EntityTypeBuilder<FeeRuleVersion> b)
    {
        b.ToTable("fee_rule_versions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.StateCode).HasMaxLength(2);
        b.Property(x => x.LicenseType).HasMaxLength(20);
        b.Property(x => x.LineOfBusiness).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.CalcType).IsRequired().HasMaxLength(20);
        b.Property(x => x.InstallmentBehavior).IsRequired().HasMaxLength(30);
        b.Property(x => x.PremiumThresholdBasis).HasMaxLength(20);
        b.Property(x => x.RoundingMode).IsRequired().HasMaxLength(30);
        b.Property(x => x.PayableRouting).IsRequired().HasMaxLength(20);
        b.Property(x => x.Notes).HasMaxLength(2000);

        b.Property(x => x.FlatAmount).HasColumnType("numeric(19,4)");
        b.Property(x => x.PercentRate).HasColumnType("numeric(9,6)");
        b.Property(x => x.MinimumAmount).HasColumnType("numeric(19,4)");
        b.Property(x => x.MaxPercent).HasColumnType("numeric(9,6)");
        b.Property(x => x.MaxAmount).HasColumnType("numeric(19,4)");
        b.Property(x => x.PremiumMinThreshold).HasColumnType("numeric(19,4)");
        b.Property(x => x.PremiumMaxThreshold).HasColumnType("numeric(19,4)");

        b.HasOne(x => x.FeeDefinition)
            .WithMany(x => x.RuleVersions)
            .HasForeignKey(x => x.FeeDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.PayablePayee)
            .WithMany()
            .HasForeignKey(x => x.PayablePayeeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.FeeDefinitionId, x.StateCode, x.EffectiveDate })
            .HasDatabaseName("ix_fee_rule_lookup");
    }
}
