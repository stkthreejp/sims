using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Infrastructure.Data.Configurations.Accounting;

public class FeeRuleVersionConfiguration : IEntityTypeConfiguration<FeeRuleVersion>
{
    public void Configure(EntityTypeBuilder<FeeRuleVersion> b)
    {
        const string programScopeCanonicalCheck =
            """
            (
                "ProgramConfigurationId" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NULL
                AND "LineOfBusiness" IS NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NOT NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NOT NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "CarrierId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "StateCode" IS NOT NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NOT NULL
            )
            """;

        b.ToTable("fee_rule_versions", t => t.HasCheckConstraint("ck_fee_rule_program_scope_canonical", programScopeCanonicalCheck));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedOnAdd();
        b.Property(x => x.StateCode).HasMaxLength(2);
        b.Property(x => x.LicenseType).HasMaxLength(20);
        b.Property(x => x.LineOfBusiness).HasMaxLength(100);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.CalcType).IsRequired().HasMaxLength(20);
        b.Property(x => x.InstallmentBehavior).IsRequired().HasMaxLength(30);
        b.Property(x => x.PremiumThresholdBasis).HasMaxLength(20);
        b.Property(x => x.ExcludedPolicyTransactionTypes).HasMaxLength(500);
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

        b.HasOne(x => x.Carrier)
            .WithMany()
            .HasForeignKey(x => x.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(x => x.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramCarrier)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(x => x.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);

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
        b.HasIndex(x => new { x.FeeDefinitionId, x.CarrierId, x.LineOfBusiness, x.StateCode, x.EffectiveDate })
            .HasDatabaseName("ix_fee_rule_carrier_lob_lookup");
        b.HasIndex(x => new { x.FeeDefinitionId, x.ProgramConfigurationId, x.CarrierId, x.LineOfBusiness, x.StateCode, x.EffectiveDate })
            .HasDatabaseName("ix_fee_rule_program_carrier_lob_lookup");
        b.HasIndex(x => x.ProgramCarrierId)
            .HasDatabaseName("ix_fee_rule_program_carrier_scope");
        b.HasIndex(x => x.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_fee_rule_program_lob_scope");
        b.HasIndex(x => x.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_fee_rule_program_state_scope");

    }
}
