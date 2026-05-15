using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceAuditLogConfiguration : IEntityTypeConfiguration<ComplianceAuditLog>
{
    public void Configure(EntityTypeBuilder<ComplianceAuditLog> builder)
    {
        builder.ToTable("compliance_audit_logs");
        builder.Property(l => l.Action).HasMaxLength(80).IsRequired();
        builder.Property(l => l.FieldName).HasMaxLength(120);
        builder.Property(l => l.OldValue).HasMaxLength(4000);
        builder.Property(l => l.NewValue).HasMaxLength(4000);
        builder.Property(l => l.Comment).HasMaxLength(2000);
        builder.HasIndex(l => l.CreatedAt);

        builder.HasOne(l => l.Document)
            .WithMany()
            .HasForeignKey(l => l.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Version)
            .WithMany()
            .HasForeignKey(l => l.VersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
