namespace SIMS.Domain.Entities;

public class PolicyCancellationDetail : BaseEntity
{
    public Guid PolicyTransactionId { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string ReasonLabel { get; set; } = string.Empty;
    public string ReasonCategory { get; set; } = string.Empty;
    public string ReasonLanguageTemplate { get; set; } = string.Empty;
    public string ReasonInputsJson { get; set; } = "{}";
    public string ResolvedReasonLanguage { get; set; } = string.Empty;
    public DateOnly NoticeMailingDate { get; set; }
    public int NoticeRequirementDays { get; set; }
    public int MailingDays { get; set; }
    public DateOnly CancellationEffectiveDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public Guid? NoticeTemplateId { get; set; }
    public string? LegalRequirementSnapshotJson { get; set; }
    public string? ComplianceChecklistSnapshotJson { get; set; }

    public PolicyTransaction PolicyTransaction { get; set; } = null!;
    public DocumentTemplate? NoticeTemplate { get; set; }
}
