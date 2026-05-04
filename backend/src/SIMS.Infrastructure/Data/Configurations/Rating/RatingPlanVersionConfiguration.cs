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

        // Original snake_case columns from Rating_Initial migration
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted");
        builder.Property(v => v.DeletedAt).HasColumnName("deleted_at");

        builder.Property(v => v.RatingPlanId).HasColumnName("rating_plan_id");
        builder.Property(v => v.VersionNumber).HasColumnName("version_number");
        builder.Property(v => v.EffectiveDate).HasColumnName("effective_date");
        builder.Property(v => v.ExpirationDate).HasColumnName("expiration_date");
        builder.Property(v => v.Status).HasColumnName("status");
        builder.Property(v => v.PromotedAt).HasColumnName("promoted_at");
        builder.Property(v => v.PromotedById).HasColumnName("promoted_by_id");
        builder.Property(v => v.Notes).HasMaxLength(1000).HasColumnName("notes");
        builder.Property(v => v.ScheduleMin).HasPrecision(6, 4).HasColumnName("schedule_min");
        builder.Property(v => v.ScheduleMax).HasPrecision(6, 4).HasColumnName("schedule_max");
        builder.Property(v => v.MinimumPremium).HasPrecision(18, 2).HasColumnName("minimum_premium");

        // PascalCase columns added later by Rating_AddMakerCheckerFields migration — no remapping needed
        // CreatedById, LastEditedById stay as-is

        builder.HasOne(v => v.PromotedBy).WithMany()
            .HasForeignKey(v => v.PromotedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.CreatedBy).WithMany()
            .HasForeignKey(v => v.CreatedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.LastEditedBy).WithMany()
            .HasForeignKey(v => v.LastEditedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(v => v.FactorTables).WithOne(t => t.RatingPlanVersion)
            .HasForeignKey(t => t.RatingPlanVersionId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.EligibilityRules).WithOne(r => r.RatingPlanVersion)
            .HasForeignKey(r => r.RatingPlanVersionId).OnDelete(DeleteBehavior.Cascade);
    }
}
