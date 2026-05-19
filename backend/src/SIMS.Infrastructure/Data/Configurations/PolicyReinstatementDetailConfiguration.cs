using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyReinstatementDetailConfiguration : IEntityTypeConfiguration<PolicyReinstatementDetail>
{
    public void Configure(EntityTypeBuilder<PolicyReinstatementDetail> builder)
    {
        builder.ToTable("policy_reinstatement_details");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Reason).IsRequired().HasColumnType("text");
        builder.Property(d => d.Notes).HasColumnType("text");
        builder.HasIndex(d => d.PolicyTransactionId).IsUnique();

        builder.HasOne(d => d.PolicyTransaction).WithOne(t => t.ReinstatementDetail)
            .HasForeignKey<PolicyReinstatementDetail>(d => d.PolicyTransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
