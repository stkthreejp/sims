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

        builder.HasIndex(r => new { r.RatingPlanVersionId, r.EquipmentTypeId }).IsUnique();

        builder.HasOne(r => r.EquipmentType).WithMany(e => e.EligibilityRules)
            .HasForeignKey(r => r.EquipmentTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}
