using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class TaskAuditEntryConfiguration : IEntityTypeConfiguration<TaskAuditEntry>
{
    public void Configure(EntityTypeBuilder<TaskAuditEntry> builder)
    {
        builder.ToTable("task_audit_entries");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Action).HasConversion<int>();
        builder.Property(a => a.OldValue).HasMaxLength(2000);
        builder.Property(a => a.NewValue).HasMaxLength(2000);
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.TaskInstanceId);
        builder.HasIndex(a => a.Timestamp);
    }
}
