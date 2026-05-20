using SIMS.Application.Common;
using SIMS.Application.DTOs.DocumentAI;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentAiPreviewService
{
    Task<Result<DocumentAiNormalizationPreview>> PreviewSubmissionAttachmentAsync(
        Guid submissionId,
        Guid attachmentId,
        CancellationToken cancellationToken = default);
}
