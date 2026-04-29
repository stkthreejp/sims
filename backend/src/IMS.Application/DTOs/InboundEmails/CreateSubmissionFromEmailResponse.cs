using IMS.Application.DTOs.Submissions;

namespace IMS.Application.DTOs.InboundEmails;

public class CreateSubmissionFromEmailResponse
{
    public SubmissionDto Submission { get; set; } = null!;

    /// <summary>NotApplicable | Completed | Failed | DetectionFailed</summary>
    public string ExtractionStatus { get; set; } = "NotApplicable";

    public Guid EmailId { get; set; }
}
