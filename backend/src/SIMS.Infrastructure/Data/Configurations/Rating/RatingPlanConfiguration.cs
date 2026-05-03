using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class RatingPlanConfiguration : IEntityTypeConfiguration<RatingPlan>
{
    public void Configure(EntityTypeBuilder<RatingPlan> builder)
    {
        builder.ToTable("rating_plans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.FormulaKey).IsRequired().HasMaxLength(50);

        builder.HasIndex(p => new { p.LineOfBusiness, p.Name }).IsUnique();

        builder.HasMany(p => p.Versions).WithOne(v => v.RatingPlan)
            .HasForeignKey(v => v.RatingPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}
