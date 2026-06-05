using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Rating;

public class CarrierRatingAssignmentDto
{
    public Guid Id { get; set; }
    public Guid? ProgramConfigurationId { get; set; }
    public string? ProgramName { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string LineOfBusinessLabel { get; set; } = string.Empty;
    public Guid? ProgramCarrierLineOfBusinessId { get; set; }
    public Guid RatingPlanVersionId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
}

public class CarrierRatingAssignmentCreateDto
{
    public Guid? ProgramConfigurationId { get; set; }
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

public class RatingPlanListItemDto
{
    public Guid Id { get; set; }
    public PolicyLineOfBusiness Lob { get; set; }
    public string LobLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FormulaKey { get; set; } = string.Empty;
    public PlanStatus Status { get; set; }
    public int? ActiveVersionNumber { get; set; }
    public DateOnly? ActiveEffectiveDate { get; set; }
    public Guid? ActiveVersionId { get; set; }
    public int VersionCount { get; set; }
    public int AssignedCarrierCount { get; set; }
}
