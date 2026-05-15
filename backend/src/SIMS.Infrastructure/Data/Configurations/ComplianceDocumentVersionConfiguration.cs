using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceDocumentVersionConfiguration : IEntityTypeConfiguration<ComplianceDocumentVersion>
{
    public void Configure(EntityTypeBuilder<ComplianceDocumentVersion> builder)
    {
        builder.ToTable("compliance_document_versions");
        builder.Property(v => v.Status).HasMaxLength(40).IsRequired();
        builder.Property(v => v.HtmlContent).IsRequired();
        builder.Property(v => v.PlainText).IsRequired();
        builder.Property(v => v.ChangeSummary).HasMaxLength(1000);
        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();

        builder.HasOne(v => v.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.CreatedBy)
            .WithMany()
            .HasForeignKey(v => v.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.ApprovedBy)
            .WithMany()
            .HasForeignKey(v => v.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
