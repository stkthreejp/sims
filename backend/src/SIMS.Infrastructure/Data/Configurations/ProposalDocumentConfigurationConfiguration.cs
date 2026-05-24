using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ProposalDocumentConfigurationConfiguration : IEntityTypeConfiguration<ProposalDocumentConfiguration>
{
    public void Configure(EntityTypeBuilder<ProposalDocumentConfiguration> builder)
    {
        builder.ToTable("proposal_document_configurations");
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

        builder.HasOne(x => x.ProgramConfiguration).WithMany()
            .HasForeignKey(x => x.ProgramConfigurationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Carrier).WithMany()
            .HasForeignKey(x => x.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DocumentTemplate).WithMany()
            .HasForeignKey(x => x.DocumentTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}
