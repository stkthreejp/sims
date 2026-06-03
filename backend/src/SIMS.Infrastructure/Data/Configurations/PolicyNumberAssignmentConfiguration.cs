using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyNumberAssignmentConfiguration : IEntityTypeConfiguration<PolicyNumberAssignment>
{
    public void Configure(EntityTypeBuilder<PolicyNumberAssignment> builder)
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

        builder.ToTable("policy_number_assignments", t => t.HasCheckConstraint("ck_policy_number_assignment_program_scope_canonical", programScopeCanonicalCheck));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.State).HasMaxLength(2);
        builder.HasIndex(a => new { a.ProgramConfigurationId, a.CarrierId, a.WritingCompanyId, a.LineOfBusiness, a.State, a.IsActive })
            .HasDatabaseName("ix_policy_number_assignments_program_lookup");
        builder.HasIndex(a => a.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_policy_number_assignment_program_lob_scope");
        builder.HasIndex(a => a.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_policy_number_assignment_program_state_scope");

        builder.HasOne(a => a.PolicyNumberSequence)
            .WithMany(s => s.Assignments)
            .HasForeignKey(a => a.PolicyNumberSequenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ProgramConfiguration)
            .WithMany()
            .HasForeignKey(a => a.ProgramConfigurationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Carrier)
            .WithMany()
            .HasForeignKey(a => a.CarrierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ProgramCarrierLineOfBusiness)
            .WithMany()
            .HasForeignKey(a => a.ProgramCarrierLineOfBusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ProgramCarrierLobState)
            .WithMany()
            .HasForeignKey(a => a.ProgramCarrierLobStateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
