using IMS.Application.Common;
using IMS.Application.DTOs.Attachments;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace IMS.Application.Interfaces.Services;

public interface IAttachmentService
{
    Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId);
    Task<Result<AttachmentDto>> UploadAsync(DocumentEntityType entityType, Guid entityId, IFormFile file, DocumentType documentType, string? description, Guid userId);
    Task<Result<string>> GetDownloadUrlAsync(Guid id);
    Task<Result> DeleteAsync(Guid id, Guid userId);
}
