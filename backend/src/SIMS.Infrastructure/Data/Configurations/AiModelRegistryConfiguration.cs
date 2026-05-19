using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AiModelRegistryConfiguration : IEntityTypeConfiguration<AiModelRegistry>
{
    public void Configure(EntityTypeBuilder<AiModelRegistry> builder)
    {
        builder.ToTable("ai_model_registry");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Provider).IsRequired().HasMaxLength(80);
        builder.Property(m => m.ModelId).IsRequired().HasMaxLength(160);
        builder.Property(m => m.DisplayName).IsRequired().HasMaxLength(160);
        builder.Property(m => m.CostNotes).HasMaxLength(500);
        builder.Property(m => m.AllowedUseCases).HasColumnType("text[]");
        builder.Property(m => m.DefaultUseCases).HasColumnType("text[]");
        builder.HasIndex(m => new { m.Provider, m.ModelId }).IsUnique();
    }
}
