using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class RatingPlanVersionImpactPreviewConfiguration : IEntityTypeConfiguration<RatingPlanVersionImpactPreview>
{
    public void Configure(EntityTypeBuilder<RatingPlanVersionImpactPreview> builder)
    {
        builder.ToTable("rating_plan_version_impact_previews");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.Property(p => p.RatingPlanVersionId).HasColumnName("rating_plan_version_id");
        builder.Property(p => p.ComputedAt).HasColumnName("computed_at");
        builder.Property(p => p.ComputedById).HasColumnName("computed_by_id");
        builder.Property(p => p.QuoteCount).HasColumnName("quote_count");
        builder.Property(p => p.TotalCurrentPremium).HasPrecision(18, 2).HasColumnName("total_current_premium");
        builder.Property(p => p.TotalNewPremium).HasPrecision(18, 2).HasColumnName("total_new_premium");
        builder.Property(p => p.TotalDeltaPct).HasPrecision(18, 4).HasColumnName("total_delta_pct");
        builder.Property(p => p.QuotesUp).HasColumnName("quotes_up");
        builder.Property(p => p.QuotesDown).HasColumnName("quotes_down");
        builder.Property(p => p.QuotesFlat).HasColumnName("quotes_flat");
        builder.Property(p => p.PreviewJson).HasColumnType("jsonb").HasColumnName("preview_json");

        builder.HasOne(p => p.RatingPlanVersion).WithMany()
            .HasForeignKey(p => p.RatingPlanVersionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ComputedBy).WithMany()
            .HasForeignKey(p => p.ComputedById).OnDelete(DeleteBehavior.Restrict);
    }
}
