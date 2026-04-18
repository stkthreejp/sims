using IMS.Application.Common;
using IMS.Application.DTOs.Attachments;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace IMS.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly IServiceProvider _sp;
    private readonly IBlobStorageService _blob;
    private readonly long _maxFileSize;

    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public AttachmentService(IServiceProvider sp, IBlobStorageService blob, IConfiguration config)
    {
        _sp = sp;
        _blob = blob;
        _maxFileSize = long.Parse(config["Storage:MaxFileSizeBytes"] ?? "52428800");
    }

    public async Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId)
    {
        var q = Db.Set<Attachment>()
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

        // Upload to Azure
        string blobPath;
        using (var stream = file.OpenReadStream())
            blobPath = await _blob.UploadAsync(stream, file.FileName, file.ContentType);

        var attachment = new Attachment
        {
            EntityType = entityType,
            DocumentType = documentType,
            FileName = file.FileName,
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

        Db.Set<Attachment>().Add(attachment);
        await Db.SaveChangesAsync();
        await Db.Entry(attachment).Reference(a => a.UploadedBy).LoadAsync();

        return Result<AttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<string>> GetDownloadUrlAsync(Guid id)
    {
        var attachment = await Db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result<string>.Failure("NOT_FOUND", "Attachment not found.");

        var url = await _blob.GetDownloadUrlAsync(attachment.BlobPath, attachment.FileName);
        return Result<string>.Success(url);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId)
    {
        var attachment = await Db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result.Failure("NOT_FOUND", "Attachment not found.");

        // Delete from Azure first
        await _blob.DeleteAsync(attachment.BlobPath);

        attachment.IsDeleted = true;
        attachment.DeletedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync();
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
