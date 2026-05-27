namespace SIMS.Application.DTOs.Submissions;

public class SubmissionLocationDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? County { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionLocationCreateDto
{
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? State { get; set; }
    public string? County { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public bool IsPrimary { get; set; }
}

public class SubmissionLocationUpdateDto : SubmissionLocationCreateDto { }
