using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyCancellationDetailConfiguration : IEntityTypeConfiguration<PolicyCancellationDetail>
{
    public void Configure(EntityTypeBuilder<PolicyCancellationDetail> builder)
    {
        builder.ToTable("policy_cancellation_details");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.ReasonCode).IsRequired().HasMaxLength(50);
        builder.Property(d => d.ReasonLabel).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ReasonCategory).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ReasonLanguageTemplate).IsRequired().HasColumnType("text");
        builder.Property(d => d.ReasonInputsJson).IsRequired().HasColumnType("text");
        builder.Property(d => d.ResolvedReasonLanguage).IsRequired().HasColumnType("text");
        builder.Property(d => d.Method).IsRequired().HasMaxLength(100);
        builder.Property(d => d.LegalRequirementSnapshotJson).HasColumnType("text");
        builder.Property(d => d.ComplianceChecklistSnapshotJson).HasColumnType("text");
        builder.HasIndex(d => d.PolicyTransactionId).IsUnique();

        builder.HasOne(d => d.PolicyTransaction).WithOne(t => t.CancellationDetail)
            .HasForeignKey<PolicyCancellationDetail>(d => d.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.NoticeTemplate).WithMany()
            .HasForeignKey(d => d.NoticeTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
