namespace SIMS.Domain.Entities;

public class PolicyNonRenewalDetail : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateOnly NoticeMailingDate { get; set; }
    public int NoticeRequirementDays { get; set; }
    public int MailingDays { get; set; }
    public DateOnly NonRenewalEffectiveDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public Guid? NoticeTemplateId { get; set; }
    public string? LegalRequirementSnapshotJson { get; set; }
    public string? ComplianceChecklistSnapshotJson { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public DocumentTemplate? NoticeTemplate { get; set; }
}
