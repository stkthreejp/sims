using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyNonRenewalDetailConfiguration : IEntityTypeConfiguration<PolicyNonRenewalDetail>
{
    public void Configure(EntityTypeBuilder<PolicyNonRenewalDetail> builder)
    {
        builder.ToTable("policy_non_renewal_details");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).IsRequired().HasColumnType("text");
        builder.Property(d => d.Method).IsRequired().HasMaxLength(100);
        builder.Property(d => d.LegalRequirementSnapshotJson).HasColumnType("text");
        builder.Property(d => d.ComplianceChecklistSnapshotJson).HasColumnType("text");
        builder.HasIndex(d => d.PolicyTransactionId).IsUnique();

        builder.HasOne(d => d.PolicyTransaction).WithOne(t => t.NonRenewalDetail)
            .HasForeignKey<PolicyNonRenewalDetail>(d => d.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.NoticeTemplate).WithMany()
            .HasForeignKey(d => d.NoticeTemplateId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
