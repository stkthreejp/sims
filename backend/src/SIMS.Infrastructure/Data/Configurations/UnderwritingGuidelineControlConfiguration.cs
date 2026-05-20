using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities;

namespace SIMS.Infrastructure.Data.Configurations;

public class UnderwritingGuidelineControlConfiguration : IEntityTypeConfiguration<UnderwritingGuidelineControl>
{
    public void Configure(EntityTypeBuilder<UnderwritingGuidelineControl> builder)
    {
        builder.ToTable("underwriting_guideline_controls");
        builder.Property(c => c.ProgramName).IsRequired().HasMaxLength(160);
        builder.Property(c => c.StateCode).IsRequired().HasMaxLength(3);
        builder.Property(c => c.RuleKey).IsRequired().HasMaxLength(120);
        builder.Property(c => c.Label).IsRequired().HasMaxLength(240);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.ConditionJson).HasColumnType("jsonb");
        builder.Property(c => c.OverridePermission).HasMaxLength(120);
        builder.Property(c => c.SourceCitation).HasMaxLength(500);
        builder.Property(c => c.AiConfidence).HasPrecision(5, 4);
        builder.Property(c => c.ReviewNotes).HasMaxLength(1000);
        builder.Property(c => c.RetirementReason).HasMaxLength(1000);

        builder.HasIndex(c => new { c.Status, c.ProgramName, c.CarrierId, c.LineOfBusiness, c.StateCode });
        builder.HasIndex(c => new { c.Status, c.ProgramId });
        builder.HasIndex(c => new { c.GuidelineDocumentId, c.RuleKey });

        builder.HasOne(c => c.GuidelineDocument)
            .WithMany(d => d.Controls)
            .HasForeignKey(c => c.GuidelineDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Program)
            .WithMany(p => p.GuidelineControls)
            .HasForeignKey(c => c.ProgramId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Carrier)
            .WithMany()
            .HasForeignKey(c => c.CarrierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.ReviewedByUser)
            .WithMany()
            .HasForeignKey(c => c.ReviewedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.PublishedByUser)
            .WithMany()
            .HasForeignKey(c => c.PublishedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.RetiredByUser)
            .WithMany()
            .HasForeignKey(c => c.RetiredByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
