using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class UnderwritingGuidelineControl : BaseEntity
{
    public Guid GuidelineDocumentId { get; set; }
    public Guid? ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string StateCode { get; set; } = "ALL";
    public UnderwritingControlItemType ItemType { get; set; }
    public UnderwritingControlStage Stage { get; set; }
    public UnderwritingControlSeverity Severity { get; set; }
    public UnderwritingControlStatus Status { get; set; } = UnderwritingControlStatus.AiSuggested;
    public string RuleKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ConditionJson { get; set; }
    public bool IsBlocking { get; set; }
    public bool OverrideAllowed { get; set; } = true;
    public string? OverridePermission { get; set; }
    public string? SourceCitation { get; set; }
    public decimal? AiConfidence { get; set; }
    public int Version { get; set; } = 1;
    public int SortOrder { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public DateTime? PublishedAt { get; set; }
    public Guid? RetiredByUserId { get; set; }
    public DateTime? RetiredAt { get; set; }
    public string? RetirementReason { get; set; }

    public UnderwritingGuidelineDocument GuidelineDocument { get; set; } = null!;
    public ProgramConfiguration? Program { get; set; }
    public Carrier? Carrier { get; set; }
    public User? ReviewedByUser { get; set; }
    public User? PublishedByUser { get; set; }
    public User? RetiredByUser { get; set; }
    public ICollection<UnderwritingGuidelineAuditLog> AuditLogs { get; set; } = new List<UnderwritingGuidelineAuditLog>();
}
