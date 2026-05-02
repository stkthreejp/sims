using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("workflow_steps");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.TriggerCondition).HasMaxLength(500);

        builder.HasOne(s => s.TaskType)
            .WithMany()
            .HasForeignKey(s => s.TaskTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.DependsOnStep)
            .WithMany()
            .HasForeignKey(s => s.DependsOnStepId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.WorkflowTemplateId, s.StepOrder });
    }
}
