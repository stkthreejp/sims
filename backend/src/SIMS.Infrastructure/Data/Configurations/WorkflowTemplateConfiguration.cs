using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.ToTable("workflow_templates");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(200);
        builder.Property(w => w.Description).HasMaxLength(1000);
        builder.Property(w => w.EntityType).HasConversion<int>();

        builder.HasOne(w => w.TriggerEvent)
            .WithMany()
            .HasForeignKey(w => w.TriggerEventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(w => w.Steps)
            .WithOne(s => s.WorkflowTemplate)
            .HasForeignKey(s => s.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.TriggerEventId, w.EntityType, w.IsActive });
    }
}
