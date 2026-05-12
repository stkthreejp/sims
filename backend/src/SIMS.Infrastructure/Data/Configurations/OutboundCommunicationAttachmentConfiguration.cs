using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class OutboundCommunicationAttachmentConfiguration : IEntityTypeConfiguration<OutboundCommunicationAttachment>
{
    public void Configure(EntityTypeBuilder<OutboundCommunicationAttachment> builder)
    {
        builder.ToTable("outbound_communication_attachments");
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.OutboundCommunicationId, a.AttachmentId, a.IsDeleted });

        builder.HasOne(a => a.OutboundCommunication).WithMany(c => c.Attachments)
            .HasForeignKey(a => a.OutboundCommunicationId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Attachment).WithMany()
            .HasForeignKey(a => a.AttachmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
