using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyFormTemplateConfiguration : IEntityTypeConfiguration<PolicyFormTemplate>
{
    public void Configure(EntityTypeBuilder<PolicyFormTemplate> builder)
    {
        builder.ToTable("policy_form_templates");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FormNumber).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(250).IsRequired();
        builder.Property(f => f.EditionDate).HasMaxLength(50);
        builder.Property(f => f.FileName).HasMaxLength(255);
        builder.Property(f => f.ContentType).HasMaxLength(100);
        builder.Property(f => f.StoragePath).HasMaxLength(1000);
        builder.Property(f => f.Notes).HasMaxLength(1000);

        builder.HasIndex(f => new { f.FormNumber, f.EditionDate, f.IsDeleted });
    }
}
