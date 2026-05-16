using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyNumberSequenceUsageConfiguration : IEntityTypeConfiguration<PolicyNumberSequenceUsage>
{
    public void Configure(EntityTypeBuilder<PolicyNumberSequenceUsage> builder)
    {
        builder.ToTable("policy_number_sequence_usages");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.BasePolicyNumber).IsRequired().HasMaxLength(50);
        builder.Property(u => u.FullPolicyNumber).IsRequired().HasMaxLength(50);
        builder.HasIndex(u => u.FullPolicyNumber).IsUnique();
        builder.HasIndex(u => u.QuoteId);
        builder.HasIndex(u => u.PolicyId);

        builder.HasOne(u => u.PolicyNumberSequence)
            .WithMany(s => s.Usages)
            .HasForeignKey(u => u.PolicyNumberSequenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.PolicyNumberAssignment)
            .WithMany()
            .HasForeignKey(u => u.PolicyNumberAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Quote)
            .WithMany()
            .HasForeignKey(u => u.QuoteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Policy)
            .WithMany()
            .HasForeignKey(u => u.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.AssignedBy)
            .WithMany()
            .HasForeignKey(u => u.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
