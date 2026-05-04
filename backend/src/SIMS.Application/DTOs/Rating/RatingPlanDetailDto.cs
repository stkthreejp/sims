using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Rating;

// ─── Plan detail ──────────────────────────────────────────────────────────────

public class RatingPlanDetailDto
{
    public Guid Id { get; set; }
    public PolicyLineOfBusiness Lob { get; set; }
    public string LobLabel { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FormulaKey { get; set; } = string.Empty;
    public PlanStatus Status { get; set; }
    public List<RatingPlanVersionSummaryDto> Versions { get; set; } = [];
    public List<PlanCarrierAssignmentDto> Assignments { get; set; } = [];
}

public class RatingPlanVersionSummaryDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public PlanStatus Status { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }
    public DateTime? PromotedAt { get; set; }
    public string? PromotedByName { get; set; }
    public int AssignedCarrierCount { get; set; }
}

public class PlanCarrierAssignmentDto
{
    public Guid AssignmentId { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public Guid VersionId { get; set; }
    public int VersionNumber { get; set; }
}

// ─── Version detail ───────────────────────────────────────────────────────────

public class RatingPlanVersionDetailDto
{
    public Guid Id { get; set; }
    public Guid RatingPlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public PolicyLineOfBusiness Lob { get; set; }
    public string LobLabel { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public PlanStatus Status { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal ScheduleMin { get; set; }
    public decimal ScheduleMax { get; set; }
    public decimal? MinimumPremium { get; set; }
    public string? Notes { get; set; }
    public DateTime? PromotedAt { get; set; }
    public string? PromotedByName { get; set; }
    public Guid? PromotedById { get; set; }
}

// ─── Factors ─────────────────────────────────────────────────────────────────

public class FactorTableDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string[] DimensionNames { get; set; } = [];
    public FactorKind ValueSemantics { get; set; }
    public List<FactorRowDto> Rows { get; set; } = [];
}

public class FactorRowDto
{
    public Guid Id { get; set; }
    public Dictionary<string, string> DimensionValues { get; set; } = [];
    public decimal Factor { get; set; }
}

// ─── Eligibility rules ────────────────────────────────────────────────────────

public class EligibilityRuleDto
{
    public Guid Id { get; set; }
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
    public int TypeNumber { get; set; }
    public bool Accepted { get; set; }
}
