using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IMS.Infrastructure.Data.Configurations;

public class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    public void Configure(EntityTypeBuilder<EmailAttachment> builder)
    {
        builder.ToTable("email_attachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).HasMaxLength(200);
        builder.Property(a => a.BlobUrl).IsRequired().HasMaxLength(1000);

        builder.HasOne(a => a.InboundEmail).WithMany(e => e.Attachments)
            .HasForeignKey(a => a.InboundEmailId).OnDelete(DeleteBehavior.Cascade);
    }
}
