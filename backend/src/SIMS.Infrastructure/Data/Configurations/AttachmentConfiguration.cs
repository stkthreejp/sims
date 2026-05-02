using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.BlobPath).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Description).HasMaxLength(500);

        // Optional FK — only one will be populated per row
        builder.HasOne(a => a.Quote).WithMany(q => q.Attachments)
            .HasForeignKey(a => a.QuoteId).OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.Submission).WithMany()
            .HasForeignKey(a => a.SubmissionId).OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.Carrier).WithMany()
            .HasForeignKey(a => a.CarrierId).OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.Agent).WithMany()
            .HasForeignKey(a => a.AgentId).OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);

        builder.HasOne(a => a.UploadedBy).WithMany()
            .HasForeignKey(a => a.UploadedById).OnDelete(DeleteBehavior.Restrict);
    }
}
