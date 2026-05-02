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

    public Submission Submission { get; set; } = null!;
}
