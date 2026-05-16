using SIMS.Application.Common;
using SIMS.Application.DTOs.Compliance;
using Microsoft.AspNetCore.Http;

namespace SIMS.Application.Interfaces.Services;

public interface IComplianceDocumentService
{
    Task<ComplianceDocumentSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceDocumentListItemDto>> GetDocumentsAsync(
        string? status = null,
        string? category = null,
        string? search = null,
        CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> GetDocumentAsync(Guid id, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> CreateDocumentAsync(ComplianceDocumentCreateDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> UpdateDocumentAsync(Guid id, ComplianceDocumentUpdateDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> SaveDraftAsync(Guid id, ComplianceDraftSaveDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> SubmitForReviewAsync(Guid id, ComplianceWorkflowActionDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> RequireChangesAsync(Guid id, ComplianceWorkflowActionDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentDetailDto>> PublishDraftAsync(Guid id, CompliancePublishDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceDocumentReviewDto>> AddReviewAsync(Guid id, ComplianceReviewCreateDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceEvidenceDto>> AddEvidenceAsync(Guid id, ComplianceEvidenceCreateDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceEvidenceAttachmentDto>> UploadEvidenceAttachmentAsync(Guid evidenceId, IFormFile file, string? description, Guid userId, CancellationToken ct = default);
    Task<Result<string>> GetEvidenceAttachmentDownloadUrlAsync(Guid attachmentId, Guid userId, CancellationToken ct = default);
    Task<Result> DeleteEvidenceAttachmentAsync(Guid attachmentId, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceVersionCompareDto>> CompareVersionsAsync(Guid id, Guid? fromVersionId = null, Guid? toVersionId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceAuditLogDto>> GetAuditLogAsync(Guid documentId, CancellationToken ct = default);
    Task<IReadOnlyList<ComplianceAttestationCampaignDto>> GetAttestationCampaignsAsync(Guid? documentId = null, CancellationToken ct = default);
    Task<Result<ComplianceAttestationCampaignDto>> CreateAttestationCampaignAsync(Guid documentId, ComplianceAttestationCampaignCreateDto dto, Guid userId, CancellationToken ct = default);
    Task<Result<ComplianceAttestationCampaignDto>> GetAttestationCampaignAsync(Guid campaignId, CancellationToken ct = default);
    Task<Result<ComplianceAttestationRecipientDto>> SubmitAttestationAsync(Guid campaignId, ComplianceAttestationSubmitDto dto, Guid userId, CancellationToken ct = default);
}
