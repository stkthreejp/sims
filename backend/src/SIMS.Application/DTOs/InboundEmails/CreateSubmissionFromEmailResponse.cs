using SIMS.Application.DTOs.Submissions;

namespace SIMS.Application.DTOs.InboundEmails;

public class CreateSubmissionFromEmailResponse
{
    public SubmissionDto Submission { get; set; } = null!;

    /// <summary>NotApplicable | Completed | Failed | DetectionFailed</summary>
    public string ExtractionStatus { get; set; } = "NotApplicable";

    public Guid EmailId { get; set; }
}
