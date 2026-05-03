using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SIMS.Domain.Entities.Rating;

namespace SIMS.Infrastructure.Data.Configurations.Rating;

public class CarrierRatingAssignmentConfiguration : IEntityTypeConfiguration<CarrierRatingAssignment>
{
    public void Configure(EntityTypeBuilder<CarrierRatingAssignment> builder)
    {
        builder.ToTable("carrier_rating_assignments");
        builder.HasKey(a => a.Id);

        builder.HasIndex(a => new { a.CarrierId, a.LineOfBusiness }).IsUnique();

        builder.HasOne(a => a.Carrier).WithMany()
            .HasForeignKey(a => a.CarrierId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.RatingPlanVersion).WithMany()
            .HasForeignKey(a => a.RatingPlanVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}
