namespace SIMS.Domain.Entities;

public class SubmissionPriorCarrier : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public string? LineOfBusiness { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? Premium { get; set; }

    public Submission Submission { get; set; } = null!;
}
