namespace SIMS.Application.DTOs.Submissions;

public class SubmissionIMCoveragesDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public decimal? ScheduledEquipmentTotalLimit { get; set; }
    public decimal? UnscheduledEquipmentLimit { get; set; }
    public decimal? MaximumValueAnyOneItem { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? CoinsurancePercentage { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SubmissionIMCoveragesUpsertDto
{
    public decimal? ScheduledEquipmentTotalLimit { get; set; }
    public decimal? UnscheduledEquipmentLimit { get; set; }
    public decimal? MaximumValueAnyOneItem { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? CoinsurancePercentage { get; set; }
}

public class SubmissionEquipmentDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int ItemNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionEquipmentCreateDto
{
    public int ItemNumber { get; set; }
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public string? Description { get; set; }
    public string? SerialNumber { get; set; }
    public decimal? Value { get; set; }
}

public class SubmissionEquipmentUpdateDto : SubmissionEquipmentCreateDto { }
