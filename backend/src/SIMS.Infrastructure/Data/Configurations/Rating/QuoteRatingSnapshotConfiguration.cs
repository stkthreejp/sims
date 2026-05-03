using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class QuoteRatingSnapshotConfiguration : IEntityTypeConfiguration<QuoteRatingSnapshot>
{
    public void Configure(EntityTypeBuilder<QuoteRatingSnapshot> builder)
    {
        builder.ToTable("quote_rating_snapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ManualPremium).HasPrecision(18, 2);
        builder.Property(s => s.ScheduleModifier).HasPrecision(6, 4);
        builder.Property(s => s.ScheduleModifierReason).HasMaxLength(500);
        builder.Property(s => s.EndorsementPremium).HasPrecision(18, 2);
        builder.Property(s => s.GrandTotalPremium).HasPrecision(18, 2);

        builder.HasIndex(s => s.QuoteId);

        builder.HasOne(s => s.Quote).WithMany()
            .HasForeignKey(s => s.QuoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RatingPlanVersion).WithMany()
            .HasForeignKey(s => s.RatingPlanVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RatedBy).WithMany()
            .HasForeignKey(s => s.RatedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Lines).WithOne(l => l.Snapshot)
            .HasForeignKey(l => l.QuoteRatingSnapshotId).OnDelete(DeleteBehavior.Cascade);
    }
}
