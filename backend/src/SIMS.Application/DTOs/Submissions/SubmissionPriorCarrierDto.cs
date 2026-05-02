namespace SIMS.Application.DTOs.Submissions;

public class SubmissionPriorCarrierDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string? LineOfBusiness { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? Premium { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionPriorCarrierCreateDto
{
    public string? LineOfBusiness { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string? PolicyNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? Premium { get; set; }
}

public class SubmissionPriorCarrierUpdateDto : SubmissionPriorCarrierCreateDto { }
