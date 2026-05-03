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
        builder.Property(l => l.ExposureRef).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Inputs).HasColumnType("jsonb");
        builder.Property(l => l.FactorsApplied).HasColumnType("jsonb");
        builder.Property(l => l.LinePremium).HasPrecision(18, 2);
    }
}
