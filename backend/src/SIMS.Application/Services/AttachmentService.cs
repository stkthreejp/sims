using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SIMS.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _db;
    private readonly IBlobStorageService _blob;
    private readonly long _maxFileSize;

    public AttachmentService(Microsoft.EntityFrameworkCore.DbContext db, IBlobStorageService blob, IConfiguration config)
    {
        _db = db;
        _blob = blob;
        _maxFileSize = long.TryParse(config["Storage:MaxFileSizeBytes"], out var parsed) ? parsed : 52_428_800L;
    }

    public async Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId)
    {
        var q = _db.Set<Attachment>()
            .Include(a => a.UploadedBy)
            .Where(a => a.EntityType == entityType);

        q = entityType switch
        {
            DocumentEntityType.Policy     => q.Where(a => a.QuoteId == entityId),
            DocumentEntityType.Submission => q.Where(a => a.SubmissionId == entityId),
            DocumentEntityType.Carrier    => q.Where(a => a.CarrierId == entityId),
            DocumentEntityType.Agent      => q.Where(a => a.AgentId == entityId),
            _ => q,
        };

        var attachments = await q.OrderBy(a => a.DocumentType).ThenByDescending(a => a.CreatedAt).ToListAsync();
        return attachments.Select(MapToDto);
    }

    public async Task<Result<AttachmentDto>> UploadAsync(
        DocumentEntityType entityType, Guid entityId,
        IFormFile file, DocumentType documentType,
        string? description, Guid userId)
    {
        if (file.Length == 0)
            return Result<AttachmentDto>.Failure("EMPTY_FILE", "File is empty.");

        if (file.Length > _maxFileSize)
            return Result<AttachmentDto>.Failure("FILE_TOO_LARGE", $"File exceeds the {_maxFileSize / 1024 / 1024}MB limit.");

        // Strip directory components and non-printable characters from the name before storing.
        // The blob path itself is GUID-based (safe); this name is only used in Content-Disposition.
        var safeFileName = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(file.FileName), @"[^\w.\-() ]", "_");

        // Upload to Azure
        string blobPath;
        using (var stream = file.OpenReadStream())
            blobPath = await _blob.UploadAsync(stream, safeFileName, file.ContentType);

        var attachment = new Attachment
        {
            EntityType = entityType,
            DocumentType = documentType,
            FileName = safeFileName,
            BlobPath = blobPath,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            Description = description,
            UploadedById = userId,
        };

        // Set the correct FK
        switch (entityType)
        {
            case DocumentEntityType.Policy:     attachment.QuoteId = entityId;      break;
            case DocumentEntityType.Submission: attachment.SubmissionId = entityId; break;
            case DocumentEntityType.Carrier:    attachment.CarrierId = entityId;    break;
            case DocumentEntityType.Agent:      attachment.AgentId = entityId;      break;
        }

        _db.Set<Attachment>().Add(attachment);
        await _db.SaveChangesAsync();
        await _db.Entry(attachment).Reference(a => a.UploadedBy).LoadAsync();

        return Result<AttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<string>> GetDownloadUrlAsync(Guid id)
    {
        var attachment = await _db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result<string>.Failure("NOT_FOUND", "Attachment not found.");

        var url = await _blob.GetDownloadUrlAsync(attachment.BlobPath, attachment.FileName);
        return Result<string>.Success(url);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId)
    {
        var attachment = await _db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result.Failure("NOT_FOUND", "Attachment not found.");

        // Delete from Azure first
        await _blob.DeleteAsync(attachment.BlobPath);

        attachment.IsDeleted = true;
        attachment.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Result.Success();
    }

    private static AttachmentDto MapToDto(Attachment a) => new()
    {
        Id = a.Id,
        EntityType = a.EntityType,
        DocumentType = a.DocumentType,
        FileName = a.FileName,
        ContentType = a.ContentType,
        FileSizeBytes = a.FileSizeBytes,
        Description = a.Description,
        UploadedById = a.UploadedById,
        UploadedByName = a.UploadedBy?.FullName ?? "",
        CreatedAt = a.CreatedAt,
    };
}
