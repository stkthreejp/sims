using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace SIMS.Application.Services;

public class AttachmentService : IAttachmentService
{
    private readonly Microsoft.EntityFrameworkCore.DbContext _db;
    private readonly IBlobStorageService _blob;
    private readonly IFileScanService _fileScan;
    private readonly long _maxFileSize;
    private readonly HashSet<string> _allowedExtensions;
    private readonly Dictionary<string, string> _contentTypesByExtension;

    public AttachmentService(
        Microsoft.EntityFrameworkCore.DbContext db,
        IBlobStorageService blob,
        IFileScanService fileScan,
        IConfiguration config)
    {
        _db = db;
        _blob = blob;
        _fileScan = fileScan;
        _maxFileSize = long.TryParse(config["Storage:MaxFileSizeBytes"], out var parsed) ? parsed : 52_428_800L;
        _allowedExtensions = (config.GetSection("Storage:AllowedExtensions").Get<string[]>()
                ?? [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".html"])
            .Select(e => e.StartsWith('.') ? e.ToLowerInvariant() : $".{e.ToLowerInvariant()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _contentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".doc"] = "application/msword",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xls"] = "application/vnd.ms-excel",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".txt"] = "text/plain",
            [".csv"] = "text/csv",
            [".html"] = "text/html",
        };
    }

    public async Task<IEnumerable<AttachmentDto>> GetByEntityAsync(DocumentEntityType entityType, Guid entityId, Guid userId)
    {
        if (!await CanAccessEntityAsync(entityType, entityId, userId))
            return [];

        var q = _db.Set<Attachment>()
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .Include(a => a.PolicyVersion)
            .Where(a => a.EntityType == entityType);

        q = entityType switch
        {
            DocumentEntityType.Policy     => q.Where(a => a.QuoteId == entityId),
            DocumentEntityType.Submission => q.Where(a => a.SubmissionId == entityId),
            DocumentEntityType.Carrier    => q.Where(a => a.CarrierId == entityId),
            DocumentEntityType.Agent      => q.Where(a => a.AgentId == entityId),
            DocumentEntityType.Insured    => q.Where(a => a.InsuredId == entityId),
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
        if (!await CanAccessEntityAsync(entityType, entityId, userId))
            return Result<AttachmentDto>.Failure("ATTACHMENT_ACCESS_DENIED", "You do not have access to this attachment target.");

        if (file.Length == 0)
            return Result<AttachmentDto>.Failure("EMPTY_FILE", "File is empty.");

        if (file.Length > _maxFileSize)
            return Result<AttachmentDto>.Failure("FILE_TOO_LARGE", $"File exceeds the {_maxFileSize / 1024 / 1024}MB limit.");

        // Strip directory components and non-printable characters from the name before storing.
        // The blob path itself is GUID-based (safe); this name is only used in Content-Disposition.
        var safeFileName = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(file.FileName), @"[^\w.\-() ]", "_");
        if (string.IsNullOrWhiteSpace(safeFileName))
            return Result<AttachmentDto>.Failure("UNSUPPORTED_FILE_TYPE", "File name is required.");

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension) || !_contentTypesByExtension.TryGetValue(extension, out var contentType))
            return Result<AttachmentDto>.Failure("UNSUPPORTED_FILE_TYPE", "This file type is not allowed.");

        if (!await HasExpectedSignatureAsync(file, extension))
            return Result<AttachmentDto>.Failure("INVALID_FILE_SIGNATURE", "File contents do not match the file type.");

        var scan = await _fileScan.ScanAsync(file);
        if (!scan.IsAllowed)
            return Result<AttachmentDto>.Failure(scan.ErrorCode ?? "FILE_SCAN_FAILED", scan.ErrorMessage ?? "The uploaded file could not be scanned.");

        // Upload to Azure
        string blobPath;
        using (var stream = file.OpenReadStream())
            blobPath = await _blob.UploadAsync(stream, safeFileName, contentType);

        var attachment = new Attachment
        {
            EntityType = entityType,
            DocumentType = documentType,
            FileName = safeFileName,
            BlobPath = blobPath,
            ContentType = contentType,
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
            case DocumentEntityType.Insured:    attachment.InsuredId = entityId;    break;
        }

        _db.Set<Attachment>().Add(attachment);
        await _db.SaveChangesAsync();
        await _db.Entry(attachment).Reference(a => a.UploadedBy).LoadAsync();

        return Result<AttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<AttachmentDto>> CreateGeneratedAsync(
        DocumentEntityType entityType,
        Guid entityId,
        Stream content,
        string fileName,
        string contentType,
        long fileSizeBytes,
        DocumentType documentType,
        string? description,
        Guid userId,
        Guid? policyVersionId = null,
        Guid? policyTransactionId = null)
    {
        if (!await CanAccessEntityAsync(entityType, entityId, userId))
            return Result<AttachmentDto>.Failure("ATTACHMENT_ACCESS_DENIED", "You do not have access to this attachment target.");

        if (fileSizeBytes == 0)
            return Result<AttachmentDto>.Failure("EMPTY_FILE", "File is empty.");

        if (fileSizeBytes > _maxFileSize)
            return Result<AttachmentDto>.Failure("FILE_TOO_LARGE", $"File exceeds the {_maxFileSize / 1024 / 1024}MB limit.");

        var safeFileName = System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(fileName), @"[^\w.\-() ]", "_");
        if (string.IsNullOrWhiteSpace(safeFileName))
            return Result<AttachmentDto>.Failure("UNSUPPORTED_FILE_TYPE", "File name is required.");

        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension) || !_contentTypesByExtension.TryGetValue(extension, out var expectedContentType))
            return Result<AttachmentDto>.Failure("UNSUPPORTED_FILE_TYPE", "This file type is not allowed.");

        if (!string.Equals(contentType, expectedContentType, StringComparison.OrdinalIgnoreCase))
            return Result<AttachmentDto>.Failure("UNSUPPORTED_FILE_TYPE", "File content type is not allowed.");

        if (content.CanSeek)
            content.Position = 0;

        var blobPath = await _blob.UploadAsync(content, safeFileName, contentType);

        var attachment = new Attachment
        {
            EntityType = entityType,
            DocumentType = documentType,
            FileName = safeFileName,
            BlobPath = blobPath,
            ContentType = contentType,
            FileSizeBytes = fileSizeBytes,
            Description = description,
            PolicyTransactionId = policyTransactionId,
            PolicyVersionId = policyVersionId,
            UploadedById = userId,
        };

        switch (entityType)
        {
            case DocumentEntityType.Policy:     attachment.QuoteId = entityId;      break;
            case DocumentEntityType.Submission: attachment.SubmissionId = entityId; break;
            case DocumentEntityType.Carrier:    attachment.CarrierId = entityId;    break;
            case DocumentEntityType.Agent:      attachment.AgentId = entityId;      break;
            case DocumentEntityType.Insured:    attachment.InsuredId = entityId;    break;
        }

        _db.Set<Attachment>().Add(attachment);
        await _db.SaveChangesAsync();
        await _db.Entry(attachment).Reference(a => a.UploadedBy).LoadAsync();
        if (attachment.PolicyVersionId.HasValue)
            await _db.Entry(attachment).Reference(a => a.PolicyVersion).LoadAsync();

        return Result<AttachmentDto>.Success(MapToDto(attachment));
    }

    public async Task<Result<string>> GetDownloadUrlAsync(Guid id, Guid userId)
    {
        var attachment = await _db.Set<Attachment>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result<string>.Failure("NOT_FOUND", "Attachment not found.");

        if (!await CanAccessAttachmentAsync(attachment, userId))
            return Result<string>.Failure("ATTACHMENT_ACCESS_DENIED", "You do not have access to this attachment.");

        var url = await _blob.GetDownloadUrlAsync(attachment.BlobPath, attachment.FileName);
        return Result<string>.Success(url);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId)
    {
        var attachment = await _db.Set<Attachment>().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null)
            return Result.Failure("NOT_FOUND", "Attachment not found.");

        if (!await CanAccessAttachmentAsync(attachment, userId))
            return Result.Failure("ATTACHMENT_ACCESS_DENIED", "You do not have access to this attachment.");

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
        PolicyTransactionId = a.PolicyTransactionId,
        PolicyVersionId = a.PolicyVersionId,
        PolicyVersionNumber = a.PolicyVersion?.VersionNumber,
        FileName = a.FileName,
        ContentType = a.ContentType,
        FileSizeBytes = a.FileSizeBytes,
        Description = a.Description,
        UploadedById = a.UploadedById,
        UploadedByName = a.UploadedBy?.FullName ?? "",
        CreatedAt = a.CreatedAt,
    };

    private async Task<bool> CanAccessAttachmentAsync(Attachment attachment, Guid userId)
        => attachment.EntityType switch
        {
            DocumentEntityType.Policy when attachment.QuoteId.HasValue =>
                await CanAccessEntityAsync(DocumentEntityType.Policy, attachment.QuoteId.Value, userId),
            DocumentEntityType.Submission when attachment.SubmissionId.HasValue =>
                await CanAccessEntityAsync(DocumentEntityType.Submission, attachment.SubmissionId.Value, userId),
            DocumentEntityType.Carrier when attachment.CarrierId.HasValue =>
                await CanAccessEntityAsync(DocumentEntityType.Carrier, attachment.CarrierId.Value, userId),
            DocumentEntityType.Agent when attachment.AgentId.HasValue =>
                await CanAccessEntityAsync(DocumentEntityType.Agent, attachment.AgentId.Value, userId),
            DocumentEntityType.Insured when attachment.InsuredId.HasValue =>
                await CanAccessEntityAsync(DocumentEntityType.Insured, attachment.InsuredId.Value, userId),
            _ => false,
        };

    private async Task<bool> CanAccessEntityAsync(DocumentEntityType entityType, Guid entityId, Guid userId)
    {
        if (await HasElevatedAttachmentAccessAsync(userId))
            return await EntityExistsAsync(entityType, entityId);

        return entityType switch
        {
            DocumentEntityType.Submission => await _db.Set<Submission>().AsNoTracking().AnyAsync(s =>
                s.Id == entityId &&
                (s.CreatedById == userId || s.UnderwriterId == userId || s.AssistantUWId == userId)),
            DocumentEntityType.Policy => await _db.Set<Quote>().AsNoTracking().AnyAsync(q =>
                q.Id == entityId &&
                (q.CreatedById == userId ||
                 q.Submission.CreatedById == userId ||
                 q.Submission.UnderwriterId == userId ||
                 q.Submission.AssistantUWId == userId)),
            DocumentEntityType.Carrier => await _db.Set<Carrier>().AsNoTracking().AnyAsync(c => c.Id == entityId),
            DocumentEntityType.Agent => await _db.Set<Agent>().AsNoTracking().AnyAsync(a => a.Id == entityId),
            DocumentEntityType.Insured => await _db.Set<Insured>().AsNoTracking().AnyAsync(i => i.Id == entityId),
            _ => false,
        };
    }

    private async Task<bool> EntityExistsAsync(DocumentEntityType entityType, Guid entityId)
        => entityType switch
        {
            DocumentEntityType.Submission => await _db.Set<Submission>().AsNoTracking().AnyAsync(s => s.Id == entityId),
            DocumentEntityType.Policy => await _db.Set<Quote>().AsNoTracking().AnyAsync(q => q.Id == entityId),
            DocumentEntityType.Carrier => await _db.Set<Carrier>().AsNoTracking().AnyAsync(c => c.Id == entityId),
            DocumentEntityType.Agent => await _db.Set<Agent>().AsNoTracking().AnyAsync(a => a.Id == entityId),
            DocumentEntityType.Insured => await _db.Set<Insured>().AsNoTracking().AnyAsync(i => i.Id == entityId),
            _ => false,
        };

    private async Task<bool> HasElevatedAttachmentAccessAsync(Guid userId)
    {
        var roleIds = await _db.Set<IdentityUserRole<Guid>>()
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (roleIds.Count == 0)
            return false;

        return await _db.Set<Role>()
            .AsNoTracking()
            .AnyAsync(r => roleIds.Contains(r.Id) && (r.Name == "Admin" || r.Name == "Underwriter"));
    }

    private static async Task<bool> HasExpectedSignatureAsync(IFormFile file, string extension)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[(int)Math.Min(file.Length, 8L)];
        var read = await stream.ReadAsync(buffer);

        return extension switch
        {
            ".pdf" => StartsWith(buffer, read, [0x25, 0x50, 0x44, 0x46]),
            ".png" => StartsWith(buffer, read, [0x89, 0x50, 0x4E, 0x47]),
            ".jpg" or ".jpeg" => StartsWith(buffer, read, [0xFF, 0xD8, 0xFF]),
            ".docx" or ".xlsx" => StartsWith(buffer, read, [0x50, 0x4B, 0x03, 0x04]),
            _ => true,
        };
    }

    private static bool StartsWith(byte[] buffer, int read, byte[] signature)
    {
        if (read < signature.Length)
            return false;

        for (var i = 0; i < signature.Length; i++)
        {
            if (buffer[i] != signature[i])
                return false;
        }

        return true;
    }
}
