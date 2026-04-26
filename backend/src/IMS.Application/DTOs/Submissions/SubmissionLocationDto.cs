namespace IMS.Application.DTOs.Submissions;

public class SubmissionLocationDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionLocationCreateDto
{
    public int LocationNumber { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
}

public class SubmissionLocationUpdateDto : SubmissionLocationCreateDto { }
