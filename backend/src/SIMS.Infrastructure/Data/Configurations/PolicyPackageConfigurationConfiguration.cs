using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyPackageConfigurationConfiguration : IEntityTypeConfiguration<PolicyPackageConfiguration>
{
    public void Configure(EntityTypeBuilder<PolicyPackageConfiguration> builder)
    {
        const string programScopeCanonicalCheck =
            """
            (
                "ProgramConfigurationId" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "State" IS NULL
                AND "ProgramCarrierLineOfBusinessId" IS NOT NULL
                AND "ProgramCarrierLobStateId" IS NULL
            )
            OR (
                "ProgramConfigurationId" IS NOT NULL
                AND "State" IS NOT NULL
                AND "ProgramCarrierLineOfBusinessId" IS NULL
                AND "ProgramCarrierLobStateId" IS NOT NULL
            )
            """;

        builder.ToTable("policy_package_configurations", t => t.HasCheckConstraint("ck_policy_package_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(p => p.Id);

        builder.Property(p => p.State).HasMaxLength(2);
        builder.Property(p => p.Name).HasMaxLength(250).IsRequired();

        builder.HasIndex(p => new { p.ProgramConfigurationId, p.CarrierId, p.LineOfBusiness, p.State, p.IsDeleted })
            .HasDatabaseName("ix_policy_package_program_lookup");
        builder.HasIndex(p => p.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_policy_package_program_lob_scope");
        builder.HasIndex(p => p.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_policy_package_program_state_scope");

        builder.HasOne(p => p.Carrier)
            .WithMany()
            .HasForeignKey(p => p.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(p => p.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(p => p.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(p => p.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
