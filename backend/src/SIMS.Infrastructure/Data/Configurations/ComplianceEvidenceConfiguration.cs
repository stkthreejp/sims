using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceEvidenceConfiguration : IEntityTypeConfiguration<ComplianceEvidence>
{
    public void Configure(EntityTypeBuilder<ComplianceEvidence> builder)
    {
        builder.ToTable("compliance_evidence");
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.EvidenceType).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Url).HasMaxLength(1000);

        builder.HasOne(e => e.Document)
            .WithMany(d => d.EvidenceItems)
            .HasForeignKey(e => e.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Review)
            .WithMany()
            .HasForeignKey(e => e.ReviewId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.CreatedBy)
            .WithMany()
            .HasForeignKey(e => e.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
