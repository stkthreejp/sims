using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingGuidelineAuditLogConfiguration : IEntityTypeConfiguration<UnderwritingGuidelineAuditLog>
{
    public void Configure(EntityTypeBuilder<UnderwritingGuidelineAuditLog> builder)
    {
        builder.ToTable("underwriting_guideline_audit_logs");
        builder.Property(a => a.Action).IsRequired().HasMaxLength(80);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.BeforeJson).HasColumnType("jsonb");
        builder.Property(a => a.AfterJson).HasColumnType("jsonb");

        builder.HasIndex(a => new { a.GuidelineDocumentId, a.CreatedAt });
        builder.HasIndex(a => new { a.GuidelineControlId, a.CreatedAt });

        builder.HasOne(a => a.GuidelineDocument)
            .WithMany(d => d.AuditLogs)
            .HasForeignKey(a => a.GuidelineDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.GuidelineControl)
            .WithMany(c => c.AuditLogs)
            .HasForeignKey(a => a.GuidelineControlId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.ActorUser)
            .WithMany()
            .HasForeignKey(a => a.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

