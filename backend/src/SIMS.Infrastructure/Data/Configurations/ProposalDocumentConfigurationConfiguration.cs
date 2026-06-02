using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProposalDocumentConfigurationConfiguration : IEntityTypeConfiguration<ProposalDocumentConfiguration>
{
    public void Configure(EntityTypeBuilder<ProposalDocumentConfiguration> builder)
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

        builder.ToTable("proposal_document_configurations", t =>
        {
            t.HasCheckConstraint("ck_proposal_document_program_scope_canonical", programScopeCanonicalCheck);
            t.HasCheckConstraint("ck_proposal_document_state_notice_requires_state", """("Role" <> 1 OR "State" IS NOT NULL)""");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.State).HasMaxLength(2);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.ProgramConfigurationId,
            x.CarrierId,
            x.LineOfBusiness,
            x.State,
            x.Role,
            x.DocumentTemplateId,
            x.IsDeleted,
        });
        builder.HasIndex(x => x.ProgramCarrierLineOfBusinessId)
            .HasDatabaseName("ix_proposal_document_program_lob_scope");
        builder.HasIndex(x => x.ProgramCarrierLobStateId)
            .HasDatabaseName("ix_proposal_document_program_state_scope");

        builder.HasOne(x => x.ProgramConfiguration).WithMany()
            .HasForeignKey(x => x.ProgramConfigurationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Carrier).WithMany()
            .HasForeignKey(x => x.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProgramCarrierLineOfBusiness).WithMany()
            .HasForeignKey(x => x.ProgramCarrierLineOfBusinessId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProgramCarrierLobState).WithMany()
            .HasForeignKey(x => x.ProgramCarrierLobStateId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocumentTemplate).WithMany()
            .HasForeignKey(x => x.DocumentTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
