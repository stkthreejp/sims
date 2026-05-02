using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class TaskTypeConfiguration : IEntityTypeConfiguration<TaskType>
{
    public void Configure(EntityTypeBuilder<TaskType> builder)
    {
        builder.ToTable("task_types");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.AssignedRoleTemplate).HasMaxLength(100);
        builder.Property(t => t.DueDateFormula).HasMaxLength(500);
        builder.Property(t => t.DefaultPriority).HasConversion<int>();

        builder.HasOne(t => t.ParentTaskType)
            .WithMany(t => t.ChildTaskTypes)
            .HasForeignKey(t => t.ParentTaskTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
