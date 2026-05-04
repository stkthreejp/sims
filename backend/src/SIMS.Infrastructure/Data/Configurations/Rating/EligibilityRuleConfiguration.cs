using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class EligibilityRuleConfiguration : IEntityTypeConfiguration<EligibilityRule>
{
    public void Configure(EntityTypeBuilder<EligibilityRule> builder)
    {
        builder.ToTable("eligibility_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted");
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");

        builder.Property(r => r.RatingPlanVersionId).HasColumnName("rating_plan_version_id");
        builder.Property(r => r.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(r => r.Accepted).HasColumnName("accepted");

        builder.HasIndex(r => new { r.RatingPlanVersionId, r.EquipmentTypeId }).IsUnique();

        builder.HasOne(r => r.EquipmentType).WithMany(e => e.EligibilityRules)
            .HasForeignKey(r => r.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
