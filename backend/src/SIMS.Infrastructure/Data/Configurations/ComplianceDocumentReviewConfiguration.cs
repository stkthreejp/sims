using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceDocumentReviewConfiguration : IEntityTypeConfiguration<ComplianceDocumentReview>
{
    public void Configure(EntityTypeBuilder<ComplianceDocumentReview> builder)
    {
        builder.ToTable("compliance_document_reviews");
        builder.Property(r => r.Status).HasMaxLength(40).IsRequired();
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.HasIndex(r => r.ReviewedAt);

        builder.HasOne(r => r.Document)
            .WithMany(d => d.Reviews)
            .HasForeignKey(r => r.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Version)
            .WithMany()
            .HasForeignKey(r => r.VersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
