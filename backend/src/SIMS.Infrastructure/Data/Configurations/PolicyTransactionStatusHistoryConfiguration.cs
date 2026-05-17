using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyTransactionStatusHistoryConfiguration : IEntityTypeConfiguration<PolicyTransactionStatusHistory>
{
    public void Configure(EntityTypeBuilder<PolicyTransactionStatusHistory> builder)
    {
        builder.ToTable("policy_transaction_status_history");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.EventName).IsRequired().HasMaxLength(100);
        builder.Property(h => h.Notes).HasMaxLength(2000);
        builder.HasIndex(h => new { h.PolicyTransactionId, h.ChangedAt });

        builder.HasOne(h => h.PolicyTransaction)
            .WithMany(t => t.StatusHistory)
            .HasForeignKey(h => h.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedBy)
            .WithMany()
            .HasForeignKey(h => h.ChangedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
