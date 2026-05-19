using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyRewriteDetailConfiguration : IEntityTypeConfiguration<PolicyRewriteDetail>
{
    public void Configure(EntityTypeBuilder<PolicyRewriteDetail> builder)
    {
        builder.ToTable("policy_rewrite_details");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).IsRequired().HasColumnType("text");
        builder.Property(d => d.Notes).HasColumnType("text");
        builder.HasIndex(d => d.PolicyTransactionId).IsUnique();
        builder.HasIndex(d => d.ReplacementQuoteId).IsUnique();

        builder.HasOne(d => d.PolicyTransaction).WithOne(t => t.RewriteDetail)
            .HasForeignKey<PolicyRewriteDetail>(d => d.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.SourcePolicy).WithMany()
            .HasForeignKey(d => d.SourcePolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.SourcePolicyVersion).WithMany()
            .HasForeignKey(d => d.SourcePolicyVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.ReplacementQuote).WithMany()
            .HasForeignKey(d => d.ReplacementQuoteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
