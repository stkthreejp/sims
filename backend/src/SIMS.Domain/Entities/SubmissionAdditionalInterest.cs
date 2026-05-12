using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class SubmissionAdditionalInterest : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;

    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    public AdditionalInterestAppliesToType AppliesToType { get; set; } = AdditionalInterestAppliesToType.Blanket;
    public string? ScheduledItemNumbers { get; set; }

    public bool AdditionalInsured { get; set; }
    public bool LossPayee { get; set; }
    public bool WaiverOfSubrogation { get; set; }
    public bool PrimaryNonContributory { get; set; }
    public string? Notes { get; set; }
}
