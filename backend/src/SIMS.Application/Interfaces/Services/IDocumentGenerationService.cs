using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Domain.Enums;

namespace SIMS.Application.Interfaces.Services;

public interface IDocumentGenerationService
{
    /// <summary>
    /// Fills a template with entity data, converts to PDF, stores in blob storage,
    /// saves the generated file as an attachment, and returns a signed download URL.
    /// </summary>
    Task<Result<GeneratedDocumentDto>> GenerateAsync(Guid templateId, TemplateEntityType entityType, Guid entityId, DocumentType? documentType, Guid userId);
    Task<Result<GeneratedDocumentDto>> GenerateForPolicyTransactionAsync(Guid templateId, Guid policyId, Guid policyTransactionId, DocumentType documentType, Guid userId);
}

public sealed record GeneratedDocumentDto(string Url, AttachmentDto Attachment);
