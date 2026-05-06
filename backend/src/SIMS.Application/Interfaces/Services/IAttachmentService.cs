using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace SIMS.Application.Interfaces.Services;

public interface IAttachmentService
{
    Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId, Guid userId);
    Task<Result<AttachmentDto>> UploadAsync(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description, Guid userId);
    Task<Result<string>> GetDownloadUrlAsync(Guid id, Guid userId);
    Task<Result> DeleteAsync(Guid id, Guid userId);
}
