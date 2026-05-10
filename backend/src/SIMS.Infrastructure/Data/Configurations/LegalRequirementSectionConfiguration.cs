using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class LegalRequirementSectionConfiguration : IEntityTypeConfiguration<LegalRequirementSection>
{
    public void Configure(EntityTypeBuilder<LegalRequirementSection> builder)
    {
        builder.ToTable("legal_requirement_sections");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.State).IsRequired().HasMaxLength(80);
        builder.Property(r => r.LineOfBusiness).IsRequired().HasMaxLength(80);
        builder.Property(r => r.Action).IsRequired().HasMaxLength(80);
        builder.Property(r => r.Category).IsRequired().HasMaxLength(120);
        builder.Property(r => r.Topic).IsRequired().HasMaxLength(160);
        builder.Property(r => r.RequirementText).IsRequired();
        builder.Property(r => r.Citations).HasColumnType("text[]");
        builder.Property(r => r.SourceName).IsRequired().HasMaxLength(120);
        builder.Property(r => r.SourceDocument).IsRequired().HasMaxLength(220);
        builder.Property(r => r.ReviewStatus).IsRequired().HasMaxLength(40);

        builder.HasIndex(r => new { r.State, r.Category, r.Topic });
        builder.HasIndex(r => new { r.LineOfBusiness, r.Action });
        builder.HasIndex(r => r.ReviewStatus);
    }
}
