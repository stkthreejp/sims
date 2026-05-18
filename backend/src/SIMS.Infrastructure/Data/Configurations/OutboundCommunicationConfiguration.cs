using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class OutboundCommunicationConfiguration : IEntityTypeConfiguration<OutboundCommunication>
{
    public void Configure(EntityTypeBuilder<OutboundCommunication> builder)
    {
        builder.ToTable("outbound_communications");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ToAddress).IsRequired().HasMaxLength(320);
        builder.Property(c => c.ToName).HasMaxLength(200);
        builder.Property(c => c.CcAddresses).HasMaxLength(1000);
        builder.Property(c => c.BccAddresses).HasMaxLength(1000);
        builder.Property(c => c.FromAddress).IsRequired().HasMaxLength(320);
        builder.Property(c => c.FromName).HasMaxLength(200);
        builder.Property(c => c.Subject).IsRequired().HasMaxLength(500);
        builder.Property(c => c.BodyHtml).IsRequired();
        builder.Property(c => c.Purpose)
            .HasDefaultValue(OutboundCommunicationPurpose.Other)
            .HasSentinel((OutboundCommunicationPurpose)(-1));
        builder.Property(c => c.FailureReason).HasMaxLength(1000);
        builder.Property(c => c.GraphMessageId).HasMaxLength(500);
        builder.Property(c => c.GraphMessageWebLink).HasMaxLength(2000);

        builder.HasIndex(c => new { c.EntityType, c.EntityId, c.IsDeleted });
        builder.HasIndex(c => c.PolicyTransactionId);
        builder.HasIndex(c => c.Purpose);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.TemplateId);

        builder.HasOne(c => c.PolicyTransaction).WithMany()
            .HasForeignKey(c => c.PolicyTransactionId).OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(c => c.Template).WithMany()
            .HasForeignKey(c => c.TemplateId).OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasOne(c => c.CreatedBy).WithMany()
            .HasForeignKey(c => c.CreatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.SentBy).WithMany()
            .HasForeignKey(c => c.SentById).OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
