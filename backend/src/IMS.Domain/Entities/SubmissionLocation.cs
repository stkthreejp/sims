namespace IMS.Domain.Entities;

public class SubmissionLocation : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ZipCode { get; set; }

    public Submission Submission { get; set; } = null!;
}
