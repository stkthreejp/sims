using SIMS.Application.DTOs.Accounting;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.DTOs.Tasks;
using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Policies;

public class PolicyListItemDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public string InsuredName { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal TotalPremium { get; set; }
    public PolicyStatus Status { get; set; }
    public DateOnly BoundDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PolicyDto
{
    public Guid Id { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public string InsuredName { get; set; } = string.Empty;
    public string InsuredState { get; set; } = string.Empty;
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public PolicyStatus Status { get; set; }
    public DateOnly BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }
    public DateOnly? NonRenewedDate { get; set; }
    public Guid BoundQuoteId { get; set; }

    // Commission from the bound quote (effective rates)
    public decimal CarrierCommissionRate { get; set; }
    public decimal SMMRetentionRate { get; set; }
    public decimal AgentCommissionRate { get; set; }
    public decimal CarrierCommissionAmount { get; set; }
    public decimal SMMRetentionAmount { get; set; }
    public decimal AgentCommissionAmount { get; set; }

    // Coverage from the bound quote
    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public decimal? UninsuredMotoristLimit { get; set; }
    public decimal? MedicalPaymentsLimit { get; set; }

    public IList<PolicyTransactionDto> Transactions { get; set; } = new List<PolicyTransactionDto>();
    public DateTime CreatedAt { get; set; }
}

public class PolicyTransactionDto
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public TransactionType TransactionType { get; set; }
    public PolicyTransactionStatus Status { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public Guid? SourceQuoteId { get; set; }
    public Guid? RenewalQuoteId { get; set; }
    public Guid? PriorPolicyVersionId { get; set; }
    public Guid? ResultingPolicyVersionId { get; set; }
    public PolicyVersionSummaryDto? PriorVersion { get; set; }
    public PolicyVersionSummaryDto? ResultingVersion { get; set; }
    public Guid? RequestedById { get; set; }
    public DateTime? RequestedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? IssuedById { get; set; }
    public DateTime? IssuedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonText { get; set; }
    public string? EndorsementDescription { get; set; }
    public Guid? PriorPolicyId { get; set; }
    public string? CancellationReason { get; set; }
    public string? CancellationMethod { get; set; }
    public PolicyCancellationDetailDto? CancellationDetail { get; set; }
    public PolicyNonRenewalDetailDto? NonRenewalDetail { get; set; }
    public IReadOnlyList<CancellationComplianceChecklistItemDto> CancellationComplianceChecklist { get; set; } = [];
    public string? CancellationLegalRequirementSnapshotJson { get; set; }
    public decimal? PremiumBefore { get; set; }
    public decimal PremiumChange { get; set; }
    public decimal NewTotalPremium { get; set; }
    public decimal? PremiumAfter { get; set; }
    public decimal? TaxesAndFeesDelta { get; set; }
    public decimal? CommissionDelta { get; set; }
    public string? BillingModeSnapshot { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? VoidsPolicyTransactionId { get; set; }
    public Guid? ReversesPolicyTransactionId { get; set; }
    public string ProcessedByName { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string? Notes { get; set; }
}

public class PolicyCancellationDetailDto
{
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
    public string? NoticeTemplateName { get; set; }
}

public class PolicyNonRenewalDetailDto
{
    public string Reason { get; set; } = string.Empty;
    public DateOnly NoticeMailingDate { get; set; }
    public int NoticeRequirementDays { get; set; }
    public int MailingDays { get; set; }
    public DateOnly NonRenewalEffectiveDate { get; set; }
    public string Method { get; set; } = string.Empty;
    public Guid? NoticeTemplateId { get; set; }
    public string? NoticeTemplateName { get; set; }
}

public class PolicyTransactionArtifactsDto
{
    public PolicyTransactionDto Transaction { get; set; } = new();
    public IReadOnlyList<AttachmentDto> Documents { get; set; } = [];
    public IReadOnlyList<RatingResultDto> RatingSnapshots { get; set; } = [];
    public IReadOnlyList<InvoiceSummaryDto> Invoices { get; set; } = [];
    public IReadOnlyList<OutboundCommunicationListItemDto> Communications { get; set; } = [];
    public IReadOnlyList<PolicyTransactionComplianceChecklistDto> ComplianceChecklists { get; set; } = [];
    public IReadOnlyList<PolicyTransactionApprovalDto> Approvals { get; set; } = [];
    public IReadOnlyList<TaskInstanceListItemDto> Tasks { get; set; } = [];
}

public class PolicyTransactionApprovalDto
{
    public Guid Id { get; set; }
    public Guid PolicyTransactionId { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public Guid RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public Guid? DecisionById { get; set; }
    public string? DecisionByName { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? Decision { get; set; }
    public string? Notes { get; set; }
}

public class PolicyTransactionComplianceChecklistDto
{
    public Guid Id { get; set; }
    public Guid PolicyTransactionId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public IReadOnlyList<PolicyTransactionComplianceChecklistItemDto> Items { get; set; } = [];
}

public class PolicyTransactionComplianceChecklistItemDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public Guid? LegalRequirementSectionId { get; set; }
    public Guid? CompletedById { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }
    public string? SnapshotJson { get; set; }
}

public class PolicyVersionSummaryDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public PolicyStatus Status { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public Guid? RatingSnapshotId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateEndorsementDto
{
    public DateOnly EffectiveDate { get; set; }
    public decimal PremiumChange { get; set; }
    public string? EndorsementDescription { get; set; }
    public string? Notes { get; set; }
}

public class IssueEndorsementDto
{
    // Allows overriding on issue — both optional
    public DateOnly? EffectiveDate { get; set; }
    public decimal? PremiumChange { get; set; }
}

public class PolicyIssuancePacketDto
{
    public Guid PolicyId { get; set; }
    public Guid BoundQuoteId { get; set; }
    public bool IsIssued { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public int IncludedFormCount { get; set; }
    public bool IsReady { get; set; }
    public IReadOnlyList<string> ReadinessMessages { get; set; } = [];
    public IReadOnlyList<PolicyIssuanceFormDto> Forms { get; set; } = [];
}

public class PolicyIssuanceFormDto
{
    public Guid Id { get; set; }
    public Guid PolicyFormTemplateId { get; set; }
    public string FormNumber { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string? EditionDate { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; }
    public bool IsIncluded { get; set; }
    public bool IsSystemGenerated { get; set; }
    public string? FileName { get; set; }
    public string ReadinessStatus { get; set; } = "Ready";
    public string? ReadinessMessage { get; set; }
}

public class IssuePolicyDto
{
    public DateOnly IssuedDate { get; set; }
    public string? Notes { get; set; }
}

public class VoidTestBindDto
{
    public string? Reason { get; set; }
}

public class VoidTestBindResultDto
{
    public Guid PolicyId { get; set; }
    public Guid QuoteId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public long? VoidedInvoiceId { get; set; }
    public Guid? ReversalTransactionId { get; set; }
}

public class NonRenewPolicyDto
{
    public DateOnly NonRenewedDate { get; set; }
    public string? Reason { get; set; }
    public DateOnly NoticeMailingDate { get; set; }
    public int NoticeRequirementDays { get; set; }
    public int MailingDays { get; set; }
    public string Method { get; set; } = "Written Notice";
    public Guid? NoticeTemplateId { get; set; }
}

public class CancelPolicyDto
{
    public DateOnly CancelledDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Method { get; set; } = "Written Notice";
    public decimal PremiumChange { get; set; }
    public IReadOnlyList<CancellationComplianceChecklistItemDto> ComplianceChecklist { get; set; } = [];
    public Guid[] LegalRequirementSectionIds { get; set; } = [];
    public string? Notes { get; set; }
}

public class IssueCancellationNoticeDto
{
    public string ReasonCode { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> ReasonInputs { get; set; } = new Dictionary<string, string>();
    public DateOnly NoticeMailingDate { get; set; }
    public int NoticeRequirementDays { get; set; }
    public int MailingDays { get; set; }
    public string Method { get; set; } = "Written Notice";
    public Guid? NoticeTemplateId { get; set; }
    public string? Notes { get; set; }
}

public class CompleteCancellationDto
{
    public DateOnly CompletedDate { get; set; }
    public string? Notes { get; set; }
}

public class LegalComplianceGuidanceDto
{
    public string State { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Action { get; set; } = "Cancellation";
    public IReadOnlyList<LegalComplianceRequirementDto> Requirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> NoticeRequirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> ReasonRequirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> ProofOfNoticeRequirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> LienholderRequirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> StateAuthorityRequirements { get; set; } = [];
    public IReadOnlyList<LegalComplianceRequirementDto> ReturnPremiumRequirements { get; set; } = [];
}

public class LegalComplianceRequirementDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string RequirementText { get; set; } = string.Empty;
    public string[] Citations { get; set; } = [];
    public DateTime LastVerifiedAt { get; set; }
}

public class CancellationComplianceChecklistItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public Guid[] RequirementSectionIds { get; set; } = [];
}
