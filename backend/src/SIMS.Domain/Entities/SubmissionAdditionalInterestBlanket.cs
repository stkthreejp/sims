using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class SubmissionAdditionalInterestBlanket : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public bool AdditionalInsured { get; set; }
    public bool WaiverOfSubrogation { get; set; }
    public bool PrimaryNonContributory { get; set; }
}
