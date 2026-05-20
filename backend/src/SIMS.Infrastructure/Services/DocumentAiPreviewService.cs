using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class DocumentAiPreviewService : IDocumentAiPreviewService
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly IDocumentAiExtractionService _documentAi;

    public DocumentAiPreviewService(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        IDocumentAiExtractionService documentAi)
    {
        _db = db;
        _blobStorage = blobStorage;
        _documentAi = documentAi;
    }

    public async Task<Result<DocumentAiNormalizationPreview>> PreviewSubmissionAttachmentAsync(
        Guid submissionId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await _db.Attachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == attachmentId &&
                a.SubmissionId == submissionId &&
                a.EntityType == DocumentEntityType.Submission,
                cancellationToken);

        if (attachment == null)
            return Result<DocumentAiNormalizationPreview>.Failure("SUBMISSION_ATTACHMENT_NOT_FOUND", "Submission attachment not found.");

        if (!IsPdf(attachment))
            return Result<DocumentAiNormalizationPreview>.Failure("UNSUPPORTED_DOCUMENT_TYPE", "Only PDF attachments can be previewed with Document AI.");

        var bytes = await _blobStorage.DownloadAsync(attachment.BlobPath);
        var extraction = await _documentAi.ProcessAsync(
            bytes,
            string.IsNullOrWhiteSpace(attachment.ContentType) ? "application/pdf" : attachment.ContentType,
            attachment.FileName,
            cancellationToken);

        return Result<DocumentAiNormalizationPreview>.Success(DocumentAiNormalizationService.Normalize(extraction));
    }

    private static bool IsPdf(Attachment attachment) =>
        attachment.ContentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
        || attachment.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
}
