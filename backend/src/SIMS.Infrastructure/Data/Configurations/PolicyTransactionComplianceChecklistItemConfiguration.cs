using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyTransactionComplianceChecklistItemConfiguration : IEntityTypeConfiguration<PolicyTransactionComplianceChecklistItem>
{
    public void Configure(EntityTypeBuilder<PolicyTransactionComplianceChecklistItem> builder)
    {
        builder.ToTable("policy_transaction_compliance_checklist_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Key).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Label).IsRequired().HasMaxLength(300);
        builder.Property(i => i.Notes).HasMaxLength(2000);
        builder.Property(i => i.SnapshotJson).HasColumnType("text");
        builder.HasIndex(i => i.PolicyTransactionComplianceChecklistId);
        builder.HasIndex(i => i.LegalRequirementSectionId);

        builder.HasOne(i => i.Checklist)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.PolicyTransactionComplianceChecklistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.LegalRequirementSection)
            .WithMany()
            .HasForeignKey(i => i.LegalRequirementSectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.CompletedBy)
            .WithMany()
            .HasForeignKey(i => i.CompletedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
