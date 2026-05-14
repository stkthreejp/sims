using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyFormFieldMappingConfiguration : IEntityTypeConfiguration<PolicyFormFieldMapping>
{
    public void Configure(EntityTypeBuilder<PolicyFormFieldMapping> builder)
    {
        builder.ToTable("policy_form_field_mappings");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.PdfFieldName).HasMaxLength(250).IsRequired();
        builder.Property(m => m.DataPath).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Format).HasMaxLength(100);

        builder.HasIndex(m => new { m.PolicyFormTemplateId, m.PdfFieldName, m.IsDeleted });

        builder.HasOne(m => m.PolicyFormTemplate)
            .WithMany(f => f.FieldMappings)
            .HasForeignKey(m => m.PolicyFormTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
