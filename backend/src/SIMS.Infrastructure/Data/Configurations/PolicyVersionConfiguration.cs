using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class PolicyVersionConfiguration : IEntityTypeConfiguration<PolicyVersion>
{
    public void Configure(EntityTypeBuilder<PolicyVersion> builder)
    {
        builder.ToTable("policy_versions");
        builder.HasKey(v => v.Id);

        builder.HasIndex(v => new { v.PolicyId, v.VersionNumber }).IsUnique();
        builder.Property(v => v.PremiumAmount).HasPrecision(18, 2);
        builder.Property(v => v.TaxesAndFees).HasPrecision(18, 2);
        builder.Property(v => v.TotalPremium).HasPrecision(18, 2);
        builder.Property(v => v.CoverageSnapshotJson).HasColumnType("text");
        builder.Property(v => v.ExposureSnapshotJson).HasColumnType("text");

        builder.HasOne(v => v.Policy).WithMany(p => p.Versions)
            .HasForeignKey(v => v.PolicyId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.CreatedByPolicyTransaction).WithMany()
            .HasForeignKey(v => v.CreatedByPolicyTransactionId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.PriorPolicyVersion).WithMany()
            .HasForeignKey(v => v.PriorPolicyVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.RatingSnapshot).WithMany()
            .HasForeignKey(v => v.RatingSnapshotId).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.CreatedBy).WithMany()
            .HasForeignKey(v => v.CreatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
