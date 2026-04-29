using IMS.Application.DTOs.Submissions;

namespace IMS.Application.DTOs.InboundEmails;

public class CreateSubmissionFromEmailResponse
{
    /// <summary>One submission per detected line of business. Always contains at least one entry.</summary>
    public List<SubmissionDto> Submissions { get; set; } = [];

    /// <summary>NotApplicable | Completed | Failed</summary>
    public string ExtractionStatus { get; set; } = "NotApplicable";

    public Guid EmailId { get; set; }
}
