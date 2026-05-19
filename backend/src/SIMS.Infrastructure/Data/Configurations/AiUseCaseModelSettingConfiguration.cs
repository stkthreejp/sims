using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AiUseCaseModelSettingConfiguration : IEntityTypeConfiguration<AiUseCaseModelSetting>
{
    public void Configure(EntityTypeBuilder<AiUseCaseModelSetting> builder)
    {
        builder.ToTable("ai_use_case_model_settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.UseCase).IsRequired().HasMaxLength(80);
        builder.Property(s => s.PromptVersion).IsRequired().HasMaxLength(120);
        builder.HasIndex(s => s.UseCase).IsUnique();
        builder.HasOne(s => s.AiModel)
            .WithMany()
            .HasForeignKey(s => s.AiModelRegistryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
