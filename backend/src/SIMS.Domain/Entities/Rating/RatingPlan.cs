using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Rating;

public class RatingPlan : BaseEntity
{
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FormulaKey { get; set; } = string.Empty;
    public PlanStatus Status { get; set; } = PlanStatus.Draft;

    public ICollection<RatingPlanVersion> Versions { get; set; } = new List<RatingPlanVersion>();
}
