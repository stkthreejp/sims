using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceEvidenceAttachmentConfiguration : IEntityTypeConfiguration<ComplianceEvidenceAttachment>
{
    public void Configure(EntityTypeBuilder<ComplianceEvidenceAttachment> builder)
    {
        builder.ToTable("compliance_evidence_attachments");
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.BlobPath).HasMaxLength(500).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(1000);
        builder.HasIndex(a => a.EvidenceId);

        builder.HasOne(a => a.Evidence)
            .WithMany(e => e.Attachments)
            .HasForeignKey(a => a.EvidenceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
