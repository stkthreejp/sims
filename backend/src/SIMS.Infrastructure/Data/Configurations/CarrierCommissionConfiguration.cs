using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class CarrierCommissionConfiguration : IEntityTypeConfiguration<CarrierCommission>
{
    public void Configure(EntityTypeBuilder<CarrierCommission> builder)
    {
        const string programScopeCanonicalCheck =
            """
            (
                "ProgramConfigurationId" IS NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "LineOfBusiness" IS NULL
                AND "ProgramCarrierId" IS NOT NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "LineOfBusiness" IS NOT NULL
                AND "ProgramCarrierId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NOT NULL
            )
            """;

        builder.ToTable("carrier_commissions", t => t.HasCheckConstraint("ck_carrier_commission_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CommissionRate).HasColumnType("numeric(8,6)");
        builder.Property(e => e.LineOfBusiness).HasMaxLength(50);

        builder.HasOne(e => e.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(e => e.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Carrier)
            .WithMany()
            .HasForeignKey(e => e.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProgramCarrier)
            .WithMany()
            .HasForeignKey(e => e.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(e => e.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProgramConfigurationId, e.CarrierId, e.LineOfBusiness, e.EffectiveDate })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(e => e.ProgramCarrierId)
            .HasDatabaseName("ix_carrier_commission_program_carrier_scope");
        builder.HasIndex(e => e.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_carrier_commission_program_lob_scope");
        builder.HasIndex(e => new { e.CarrierId, e.DisabledDate });
    }
}
