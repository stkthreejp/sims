using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingControlEnforcementResultConfiguration : IEntityTypeConfiguration<UnderwritingControlEnforcementResult>
{
    public void Configure(EntityTypeBuilder<UnderwritingControlEnforcementResult> builder)
    {
        builder.ToTable("underwriting_control_enforcement_results");
        builder.Property(r => r.Message).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.ConditionJson).HasColumnType("jsonb");
        builder.Property(r => r.InputSnapshotJson).HasColumnType("jsonb");
        builder.Property(r => r.OverridePermission).HasMaxLength(120);
        builder.Property(r => r.OverrideReason).HasMaxLength(1000);

        builder.HasIndex(r => new { r.TargetType, r.TargetId, r.Stage, r.Status });
        builder.HasIndex(r => new { r.GuidelineControlId, r.TargetType, r.TargetId, r.Stage }).IsUnique();

        builder.HasOne(r => r.GuidelineControl)
            .WithMany()
            .HasForeignKey(r => r.GuidelineControlId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.OverriddenByUser)
            .WithMany()
            .HasForeignKey(r => r.OverriddenByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
