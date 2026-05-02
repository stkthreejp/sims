using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class TaskInstanceConfiguration : IEntityTypeConfiguration<TaskInstance>
{
    public void Configure(EntityTypeBuilder<TaskInstance> builder)
    {
        builder.ToTable("task_instances");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Priority).HasConversion<int>();
        builder.Property(t => t.EntityType).HasConversion<int>();
        builder.Property(t => t.AssignedRoleExpression).HasMaxLength(200);
        builder.Property(t => t.ReferenceUrl).HasMaxLength(2000);

        builder.HasOne(t => t.TaskType)
            .WithMany(tt => tt.TaskInstances)
            .HasForeignKey(t => t.TaskTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.WorkflowStep)
            .WithMany()
            .HasForeignKey(t => t.WorkflowStepId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(t => t.AuditEntries)
            .WithOne(a => a.TaskInstance)
            .HasForeignKey(a => a.TaskInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.AssignedUserId);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.EntityType, t.EntityId });
        builder.HasIndex(t => t.DueDate);
    }
}
