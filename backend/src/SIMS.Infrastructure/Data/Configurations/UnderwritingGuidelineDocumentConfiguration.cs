using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingGuidelineDocumentConfiguration : IEntityTypeConfiguration<UnderwritingGuidelineDocument>
{
    public void Configure(EntityTypeBuilder<UnderwritingGuidelineDocument> builder)
    {
        builder.ToTable("underwriting_guideline_documents");
        builder.Property(d => d.ProgramName).IsRequired().HasMaxLength(160);
        builder.Property(d => d.StateCode).IsRequired().HasMaxLength(3);
        builder.Property(d => d.Title).IsRequired().HasMaxLength(240);
        builder.Property(d => d.SourceFileName).HasMaxLength(260);
        builder.Property(d => d.SourceBlobName).HasMaxLength(500);
        builder.Property(d => d.Notes).HasMaxLength(1000);

        builder.HasIndex(d => new { d.ProgramName, d.CarrierId, d.LineOfBusiness, d.StateCode, d.Version });

        builder.HasOne(d => d.Carrier)
            .WithMany()
            .HasForeignKey(d => d.CarrierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.CreatedByUser)
            .WithMany()
            .HasForeignKey(d => d.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
