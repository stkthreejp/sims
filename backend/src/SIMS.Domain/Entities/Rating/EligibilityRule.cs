namespace SIMS.Domain.Entities.Rating;

public class EligibilityRule : BaseEntity
{
    public Guid RatingPlanVersionId { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public bool Accepted { get; set; } = true;

    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
    public EquipmentType EquipmentType { get; set; } = null!;
}
