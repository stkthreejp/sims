using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Rating;

public class CarrierRatingAssignmentDto
{
    public Guid Id { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string LineOfBusinessLabel { get; set; } = string.Empty;
    public Guid RatingPlanVersionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public class CarrierRatingAssignmentCreateDto
{
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public Guid RatingPlanVersionId { get; set; }
}

public class CarrierRatingAssignmentUpdateDto
{
    public Guid RatingPlanVersionId { get; set; }
}

public class RatingPlanVersionPickerDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public PolicyLineOfBusiness Lob { get; set; }
}
