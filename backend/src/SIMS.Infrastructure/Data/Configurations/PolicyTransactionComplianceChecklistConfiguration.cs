using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyTransactionComplianceChecklistConfiguration : IEntityTypeConfiguration<PolicyTransactionComplianceChecklist>
{
    public void Configure(EntityTypeBuilder<PolicyTransactionComplianceChecklist> builder)
    {
        builder.ToTable("policy_transaction_compliance_checklists");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Purpose).IsRequired().HasMaxLength(80);
        builder.HasIndex(c => c.PolicyTransactionId);

        builder.HasOne(c => c.PolicyTransaction)
            .WithMany(t => t.ComplianceChecklists)
            .HasForeignKey(c => c.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
