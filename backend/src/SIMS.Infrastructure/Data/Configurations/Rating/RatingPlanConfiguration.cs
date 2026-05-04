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

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.Property(p => p.LineOfBusiness).HasColumnName("line_of_business");
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200).HasColumnName("name");
        builder.Property(p => p.FormulaKey).IsRequired().HasMaxLength(50).HasColumnName("formula_key");
        builder.Property(p => p.Status).HasColumnName("status");

        builder.HasIndex(p => new { p.LineOfBusiness, p.Name }).IsUnique();

        builder.HasMany(p => p.Versions).WithOne(v => v.RatingPlan)
            .HasForeignKey(v => v.RatingPlanId).OnDelete(DeleteBehavior.Cascade);
    }
}
