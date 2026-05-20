using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Underwriting;

public record UnderwritingGuidelineDocumentDto(
    Guid Id,
    string ProgramName,
    Guid? CarrierId,
    string? CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    string Title,
    string? SourceFileName,
    string? SourceBlobName,
    string? Notes,
    int Version,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    int ControlCount);

public record UnderwritingGuidelineControlDto(
    Guid Id,
    Guid GuidelineDocumentId,
    string ProgramName,
    Guid? CarrierId,
    string? CarrierName,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    UnderwritingControlItemType ItemType,
    UnderwritingControlStage Stage,
    UnderwritingControlSeverity Severity,
    UnderwritingControlStatus Status,
    string RuleKey,
    string Label,
    string? Description,
    string? ConditionJson,
    bool IsBlocking,
    bool OverrideAllowed,
    string? OverridePermission,
    string? SourceCitation,
    decimal? AiConfidence,
    int Version,
    int SortOrder,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    Guid? PublishedByUserId,
    DateTime? PublishedAt,
    Guid? RetiredByUserId,
    DateTime? RetiredAt,
    string? RetirementReason);

public record UnderwritingGuidelineAuditLogDto(
    Guid Id,
    Guid? GuidelineDocumentId,
    Guid? GuidelineControlId,
    string Action,
    Guid ActorUserId,
    string? Notes,
    string? BeforeJson,
    string? AfterJson,
    DateTime CreatedAt);

public record CreateUnderwritingGuidelineDocumentRequest(
    string ProgramName,
    Guid? CarrierId,
    PolicyLineOfBusiness LineOfBusiness,
    string StateCode,
    string Title,
    string? SourceFileName,
    string? SourceBlobName,
    string? Notes);

public record CreateUnderwritingGuidelineControlRequest(
    UnderwritingControlItemType ItemType,
    UnderwritingControlStage Stage,
    UnderwritingControlSeverity Severity,
    string RuleKey,
    string Label,
    string? Description,
    string? ConditionJson,
    bool IsBlocking,
    bool OverrideAllowed,
    string? OverridePermission,
    string? SourceCitation,
    decimal? AiConfidence,
    int SortOrder);

public record AddProposedUnderwritingControlsRequest(
    IReadOnlyList<CreateUnderwritingGuidelineControlRequest> Controls);

public record UpdateUnderwritingGuidelineControlRequest(
    UnderwritingControlItemType ItemType,
    UnderwritingControlStage Stage,
    UnderwritingControlSeverity Severity,
    string RuleKey,
    string Label,
    string? Description,
    string? ConditionJson,
    bool IsBlocking,
    bool OverrideAllowed,
    string? OverridePermission,
    string? SourceCitation,
    int SortOrder,
    string? ChangeNotes);

public record UnderwritingGuidelineDecisionRequest(string? Notes);

