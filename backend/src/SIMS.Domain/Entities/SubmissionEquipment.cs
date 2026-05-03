using SIMS.Domain.Entities.Rating;

namespace SIMS.Domain.Entities;

public class SubmissionEquipment : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int ItemNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }

    // IM rating inputs
    public Guid? EquipmentTypeId { get; set; }
    public string? TerritoryCode { get; set; }
    public decimal? Deductible { get; set; }
    public string? SettlementBasis { get; set; }

    public Submission Submission { get; set; } = null!;
    public EquipmentType? EquipmentType { get; set; }
}
