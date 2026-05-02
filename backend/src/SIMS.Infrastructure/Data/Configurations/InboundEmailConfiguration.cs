using SIMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SIMS.Infrastructure.Data.Configurations;

public class InboundEmailConfiguration : IEntityTypeConfiguration<InboundEmail>
{
    public void Configure(EntityTypeBuilder<InboundEmail> builder)
    {
        builder.ToTable("inbound_emails");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FromAddress).IsRequired().HasMaxLength(320);
        builder.Property(e => e.FromName).HasMaxLength(200);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
        builder.Property(e => e.BodyText).HasColumnType("text");
        builder.Property(e => e.GraphMessageId).HasMaxLength(500);

        builder.HasIndex(e => e.GraphMessageId).IsUnique().HasFilter("\"GraphMessageId\" IS NOT NULL");

        builder.HasOne(e => e.LinkedSubmission).WithMany()
            .HasForeignKey(e => e.LinkedSubmissionId).OnDelete(DeleteBehavior.SetNull);
    }
}
