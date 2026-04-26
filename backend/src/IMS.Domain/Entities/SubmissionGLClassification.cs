namespace IMS.Domain.Entities;

public class SubmissionGLClassification : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string? ClassCode { get; set; }
    public string? Description { get; set; }
    public string? PremiumBasis { get; set; }
    public decimal? Exposure { get; set; }

    public Submission Submission { get; set; } = null!;
}
