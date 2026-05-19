using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AiModelSettingAuditLogConfiguration : IEntityTypeConfiguration<AiModelSettingAuditLog>
{
    public void Configure(EntityTypeBuilder<AiModelSettingAuditLog> builder)
    {
        builder.ToTable("ai_model_setting_audit_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.UseCase).IsRequired().HasMaxLength(80);
        builder.Property(l => l.PreviousPromptVersion).HasMaxLength(120);
        builder.Property(l => l.NewPromptVersion).IsRequired().HasMaxLength(120);
        builder.Property(l => l.ChangeReason).IsRequired().HasMaxLength(500);
        builder.HasIndex(l => l.UseCase);
        builder.HasIndex(l => l.CreatedAt);
    }
}
