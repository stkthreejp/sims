using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class QuoteRatingLineConfiguration : IEntityTypeConfiguration<QuoteRatingLine>
{
    public void Configure(EntityTypeBuilder<QuoteRatingLine> builder)
    {
        builder.ToTable("quote_rating_lines");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted");
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");

        builder.Property(l => l.QuoteRatingSnapshotId).HasColumnName("quote_rating_snapshot_id");
        builder.Property(l => l.ExposureRef).IsRequired().HasMaxLength(100).HasColumnName("exposure_ref");
        builder.Property(l => l.Inputs).HasColumnType("jsonb").HasColumnName("inputs");
        builder.Property(l => l.FactorsApplied).HasColumnType("jsonb").HasColumnName("factors_applied");
        builder.Property(l => l.LinePremium).HasPrecision(18, 2).HasColumnName("line_premium");
    }
}
