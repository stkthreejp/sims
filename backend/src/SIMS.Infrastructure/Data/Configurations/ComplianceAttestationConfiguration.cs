using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceAttestationConfiguration :
    IEntityTypeConfiguration<ComplianceAttestationCampaign>,
    IEntityTypeConfiguration<ComplianceAttestationRecipient>
{
    public void Configure(EntityTypeBuilder<ComplianceAttestationCampaign> builder)
    {
        builder.ToTable("compliance_attestation_campaigns");
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Statement).HasMaxLength(1000).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(40).IsRequired();
        builder.HasIndex(c => c.DueDate);

        builder.HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Version)
            .WithMany()
            .HasForeignKey(c => c.VersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.CreatedBy)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }

    public void Configure(EntityTypeBuilder<ComplianceAttestationRecipient> builder)
    {
        builder.ToTable("compliance_attestation_recipients");
        builder.Property(r => r.Status).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.HasIndex(r => new { r.CampaignId, r.UserId }).IsUnique();

        builder.HasOne(r => r.Campaign)
            .WithMany(c => c.Recipients)
            .HasForeignKey(r => r.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
