using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Rating;

public class CarrierRatingAssignment : BaseEntity
{
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public Guid RatingPlanVersionId { get; set; }

    public Carrier Carrier { get; set; } = null!;
    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
}
