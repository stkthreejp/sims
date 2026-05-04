using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class ShadowRatingResultConfiguration : IEntityTypeConfiguration<ShadowRatingResult>
{
    public void Configure(EntityTypeBuilder<ShadowRatingResult> builder)
    {
        builder.ToTable("shadow_rating_results");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.Property(s => s.QuoteId).HasColumnName("quote_id");
        builder.Property(s => s.RatingPlanVersionId).HasColumnName("rating_plan_version_id");
        builder.Property(s => s.RatedAt).HasColumnName("rated_at");
        builder.Property(s => s.RatedById).HasColumnName("rated_by_id");
        builder.Property(s => s.ShadowPremium).HasPrecision(18, 2).HasColumnName("shadow_premium");
        builder.Property(s => s.ActualPremium).HasPrecision(18, 2).HasColumnName("actual_premium");
        builder.Property(s => s.DeltaAmount).HasPrecision(18, 2).HasColumnName("delta_amount");
        builder.Property(s => s.DeltaPct).HasPrecision(18, 4).HasColumnName("delta_pct");
        builder.Property(s => s.ScheduleModifier).HasPrecision(6, 4).HasColumnName("schedule_modifier");
        builder.Property(s => s.SnapshotJson).HasColumnType("jsonb").HasColumnName("snapshot_json");

        builder.HasIndex(s => s.QuoteId);
        builder.HasIndex(s => s.RatedAt);

        builder.HasOne(s => s.Quote).WithMany()
            .HasForeignKey(s => s.QuoteId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RatingPlanVersion).WithMany()
            .HasForeignKey(s => s.RatingPlanVersionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.RatedBy).WithMany()
            .HasForeignKey(s => s.RatedById).OnDelete(DeleteBehavior.Restrict);
    }
}
