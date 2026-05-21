using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;

namespace SIMS.Application.Interfaces.Services;

public interface IUnderwritingGuidelineControlService
{
    Task<IReadOnlyList<UnderwritingGuidelineDocumentDto>> GetDocumentsAsync(CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineDocumentDto>> CreateDocumentAsync(CreateUnderwritingGuidelineDocumentRequest request, Guid userId, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineDocumentDto>> UpdateDocumentAsync(Guid guidelineDocumentId, CreateUnderwritingGuidelineDocumentRequest request, Guid userId, CancellationToken ct = default);
    Task<Result> DeleteDocumentAsync(Guid guidelineDocumentId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UnderwritingGuidelineControlDto>> GetControlsAsync(Guid guidelineDocumentId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<UnderwritingGuidelineControlDto>>> AddProposedControlsAsync(Guid guidelineDocumentId, AddProposedUnderwritingControlsRequest request, Guid userId, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineControlDto>> UpdateControlAsync(Guid controlId, UpdateUnderwritingGuidelineControlRequest request, Guid userId, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineControlDto>> ApproveControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineControlDto>> RejectControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineControlDto>> PublishControlAsync(Guid controlId, Guid userId, string? notes, CancellationToken ct = default);
    Task<Result<UnderwritingGuidelineControlDto>> RetireControlAsync(Guid controlId, Guid userId, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<UnderwritingGuidelineAuditLogDto>> GetAuditLogAsync(Guid? guidelineDocumentId = null, Guid? guidelineControlId = null, CancellationToken ct = default);
}
