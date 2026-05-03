using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class RatingPlanVersionConfiguration : IEntityTypeConfiguration<RatingPlanVersion>
{
    public void Configure(EntityTypeBuilder<RatingPlanVersion> builder)
    {
        builder.ToTable("rating_plan_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Notes).HasMaxLength(1000);
        builder.Property(v => v.ScheduleMin).HasPrecision(6, 4);
        builder.Property(v => v.ScheduleMax).HasPrecision(6, 4);
        builder.Property(v => v.MinimumPremium).HasPrecision(18, 2);

        builder.HasOne(v => v.PromotedBy).WithMany()
            .HasForeignKey(v => v.PromotedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.FactorTables).WithOne(t => t.RatingPlanVersion)
            .HasForeignKey(t => t.RatingPlanVersionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.EligibilityRules).WithOne(r => r.RatingPlanVersion)
            .HasForeignKey(r => r.RatingPlanVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
