namespace IMS.Domain.Entities;

public class SubmissionIMCoverages : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public decimal? ScheduledEquipmentTotalLimit { get; set; }
    public decimal? UnscheduledEquipmentLimit { get; set; }
    public decimal? MaximumValueAnyOneItem { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? CoinsurancePercentage { get; set; }

    public Submission Submission { get; set; } = null!;
}
