using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyTransactionApprovalConfiguration : IEntityTypeConfiguration<PolicyTransactionApproval>
{
    public void Configure(EntityTypeBuilder<PolicyTransactionApproval> builder)
    {
        builder.ToTable("policy_transaction_approvals");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApprovalType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Decision).HasMaxLength(40);
        builder.Property(a => a.Notes).HasMaxLength(2000);

        builder.HasIndex(a => a.PolicyTransactionId);
        builder.HasIndex(a => new { a.PolicyTransactionId, a.ApprovalType });

        builder.HasOne(a => a.PolicyTransaction)
            .WithMany(t => t.Approvals)
            .HasForeignKey(a => a.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.RequestedBy)
            .WithMany()
            .HasForeignKey(a => a.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.DecisionBy)
            .WithMany()
            .HasForeignKey(a => a.DecisionById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
