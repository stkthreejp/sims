using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.ToTable("escalation_rules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.NotifyRoleName).IsRequired().HasMaxLength(100);

        builder.HasOne(r => r.TaskType)
            .WithMany()
            .HasForeignKey(r => r.TaskTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TaskTypeId, r.IsActive });
    }
}
