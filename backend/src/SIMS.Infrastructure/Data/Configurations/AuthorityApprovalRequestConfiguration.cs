using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class AuthorityApprovalRequestConfiguration : IEntityTypeConfiguration<AuthorityApprovalRequest>
{
    public void Configure(EntityTypeBuilder<AuthorityApprovalRequest> builder)
    {
        builder.ToTable("authority_approval_requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ActionCode).IsRequired().HasMaxLength(120);
        builder.Property(r => r.ActionLabel).IsRequired().HasMaxLength(200);
        builder.Property(r => r.RequiredPermission).IsRequired().HasMaxLength(120);
        builder.Property(r => r.ApprovalType).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Reason).IsRequired().HasMaxLength(1000);
        builder.Property(r => r.InputSnapshotJson).HasColumnType("jsonb");
        builder.Property(r => r.DecisionNotes).HasMaxLength(2000);

        builder.HasIndex(r => new { r.TargetType, r.TargetId, r.ActionCode, r.ApprovalType, r.Status });
        builder.HasIndex(r => r.AssignedToUserId);

        builder.HasOne(r => r.RequestedBy)
            .WithMany()
            .HasForeignKey(r => r.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.AssignedToUser)
            .WithMany()
            .HasForeignKey(r => r.AssignedToUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.DecisionBy)
            .WithMany()
            .HasForeignKey(r => r.DecisionById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
