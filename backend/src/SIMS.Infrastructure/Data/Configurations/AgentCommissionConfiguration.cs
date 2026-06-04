using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AgentCommissionConfiguration : IEntityTypeConfiguration<AgentCommission>
{
    public void Configure(EntityTypeBuilder<AgentCommission> builder)
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

        builder.ToTable("agent_commissions", t => t.HasCheckConstraint("ck_agent_commission_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CommissionRate).HasColumnType("numeric(8,6)");
        builder.Property(e => e.LineOfBusiness).HasMaxLength(50);
        builder.Property(e => e.StateCode).HasMaxLength(2);

        builder.HasOne(e => e.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(e => e.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Carrier)
            .WithMany()
            .HasForeignKey(e => e.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Agent)
            .WithMany()
            .HasForeignKey(e => e.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProgramCarrier)
            .WithMany()
            .HasForeignKey(e => e.ProgramCarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(e => e.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(e => e.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.ProgramConfigurationId, e.CarrierId, e.AgentId, e.LineOfBusiness, e.StateCode, e.EffectiveDate })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(e => e.ProgramCarrierId)
            .HasDatabaseName("ix_agent_commission_program_carrier_scope");
        builder.HasIndex(e => e.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_agent_commission_program_lob_scope");
        builder.HasIndex(e => e.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_agent_commission_program_state_scope");
        builder.HasIndex(e => new { e.AgentId, e.DisabledDate });
    }
}
