namespace SIMS.Domain.Entities;

public class SubmissionLocation : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? County { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsPrimary { get; set; }

    public Submission Submission { get; set; } = null!;
}
