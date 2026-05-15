using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class ComplianceDocumentConfiguration : IEntityTypeConfiguration<ComplianceDocument>
{
    public void Configure(EntityTypeBuilder<ComplianceDocument> builder)
    {
        builder.ToTable("compliance_documents");
        builder.Property(d => d.Title).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Category).HasMaxLength(80).IsRequired();
        builder.Property(d => d.DocumentType).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Status).HasMaxLength(40).IsRequired();
        builder.Property(d => d.ReviewCadence).HasMaxLength(40).IsRequired();
        builder.Property(d => d.Tags).HasColumnType("text[]");
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.NextReviewDate);

        builder.HasOne(d => d.Owner)
            .WithMany()
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Approver)
            .WithMany()
            .HasForeignKey(d => d.ApproverId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.CurrentPublishedVersion)
            .WithMany()
            .HasForeignKey(d => d.CurrentPublishedVersionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.CurrentDraftVersion)
            .WithMany()
            .HasForeignKey(d => d.CurrentDraftVersionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
