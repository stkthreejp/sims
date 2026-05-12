using SIMS.Application.Common;
using SIMS.Application.DTOs.OutboundCommunications;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class OutboundCommunicationService : IOutboundCommunicationService
{
    private readonly DbContext _db;

    public OutboundCommunicationService(DbContext db) => _db = db;

    public async Task<IEnumerable<OutboundCommunicationListItemDto>> GetForEntityAsync(
        OutboundCommunicationEntityType entityType,
        Guid entityId)
    {
        var communications = await _db.Set<OutboundCommunication>()
            .Include(c => c.CreatedBy)
            .Include(c => c.Attachments.Where(a => !a.IsDeleted))
            .Where(c => c.EntityType == entityType && c.EntityId == entityId && !c.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return communications.Select(MapToListItemDto);
    }

    public async Task<Result<OutboundCommunicationDto>> GetByIdAsync(Guid id)
    {
        var communication = await LoadByIdAsync(id);
        return communication == null
            ? Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.")
            : Result<OutboundCommunicationDto>.Success(MapToDto(communication));
    }

    public async Task<Result<OutboundCommunicationDto>> CreateDraftAsync(OutboundCommunicationCreateDto dto, Guid createdById)
    {
        var validation = await ValidateAsync(dto.TemplateId, dto.AttachmentIds);
        if (!validation.IsSuccess)
            return Result<OutboundCommunicationDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var communication = new OutboundCommunication
        {
            EntityType = dto.EntityType,
            EntityId = dto.EntityId,
            TemplateId = dto.TemplateId,
            ToAddress = dto.ToAddress.Trim(),
            ToName = dto.ToName?.Trim(),
            CcAddresses = dto.CcAddresses?.Trim(),
            BccAddresses = dto.BccAddresses?.Trim(),
            FromAddress = dto.FromAddress.Trim(),
            FromName = dto.FromName?.Trim(),
            SenderType = dto.SenderType,
            Subject = dto.Subject.Trim(),
            BodyHtml = dto.BodyHtml,
            Status = OutboundCommunicationStatus.Draft,
            CreatedById = createdById,
        };

        foreach (var attachmentId in dto.AttachmentIds.Distinct())
            communication.Attachments.Add(new OutboundCommunicationAttachment { AttachmentId = attachmentId });

        _db.Set<OutboundCommunication>().Add(communication);
        await _db.SaveChangesAsync();

        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(communication.Id))!));
    }

    public async Task<Result<OutboundCommunicationDto>> UpdateDraftAsync(Guid id, OutboundCommunicationUpdateDto dto)
    {
        var communication = await _db.Set<OutboundCommunication>()
            .Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (communication == null)
            return Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.");
        if (communication.Status != OutboundCommunicationStatus.Draft)
            return Result<OutboundCommunicationDto>.Failure("NOT_DRAFT", "Only draft communications can be edited.");

        var validation = await ValidateAsync(communication.TemplateId, dto.AttachmentIds);
        if (!validation.IsSuccess)
            return Result<OutboundCommunicationDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        communication.ToAddress = dto.ToAddress.Trim();
        communication.ToName = dto.ToName?.Trim();
        communication.CcAddresses = dto.CcAddresses?.Trim();
        communication.BccAddresses = dto.BccAddresses?.Trim();
        communication.FromAddress = dto.FromAddress.Trim();
        communication.FromName = dto.FromName?.Trim();
        communication.SenderType = dto.SenderType;
        communication.Subject = dto.Subject.Trim();
        communication.BodyHtml = dto.BodyHtml;

        var requested = dto.AttachmentIds.Distinct().ToHashSet();
        foreach (var existing in communication.Attachments)
        {
            existing.IsDeleted = !requested.Contains(existing.AttachmentId);
            existing.DeletedAt = existing.IsDeleted ? DateTime.UtcNow : null;
        }

        var existingIds = communication.Attachments.Select(a => a.AttachmentId).ToHashSet();
        foreach (var attachmentId in requested.Where(id => !existingIds.Contains(id)))
            communication.Attachments.Add(new OutboundCommunicationAttachment { AttachmentId = attachmentId });

        await _db.SaveChangesAsync();
        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(id))!));
    }

    public async Task<Result<OutboundCommunicationDto>> UpdateStatusAsync(
        Guid id,
        OutboundCommunicationStatusUpdateDto dto,
        Guid userId)
    {
        var communication = await _db.Set<OutboundCommunication>().FindAsync(id);
        if (communication == null || communication.IsDeleted)
            return Result<OutboundCommunicationDto>.Failure("NOT_FOUND", "Outbound communication not found.");

        communication.Status = dto.Status;
        communication.FailureReason = dto.FailureReason?.Trim();
        communication.GraphMessageId = dto.GraphMessageId?.Trim();

        if (dto.Status == OutboundCommunicationStatus.Sent)
        {
            communication.SentAt = DateTime.UtcNow;
            communication.SentById = userId;
        }

        await _db.SaveChangesAsync();
        return Result<OutboundCommunicationDto>.Success(MapToDto((await LoadByIdAsync(id))!));
    }

    private async Task<Result> ValidateAsync(Guid? templateId, IReadOnlyCollection<Guid> attachmentIds)
    {
        if (templateId.HasValue)
        {
            var templateExists = await _db.Set<DocumentTemplate>().AnyAsync(t => t.Id == templateId.Value && !t.IsDeleted);
            if (!templateExists)
                return Result.Failure("TEMPLATE_NOT_FOUND", "Selected template not found.");
        }

        if (attachmentIds.Count > 0)
        {
            var requested = attachmentIds.Distinct().ToList();
            var found = await _db.Set<Attachment>()
                .CountAsync(a => requested.Contains(a.Id) && !a.IsDeleted);
            if (found != requested.Count)
                return Result.Failure("ATTACHMENT_NOT_FOUND", "One or more selected attachments were not found.");
        }

        return Result.Success();
    }

    private Task<OutboundCommunication?> LoadByIdAsync(Guid id) =>
        _db.Set<OutboundCommunication>()
            .Include(c => c.CreatedBy)
            .Include(c => c.SentBy)
            .Include(c => c.Attachments.Where(a => !a.IsDeleted))
                .ThenInclude(a => a.Attachment)
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

    private static OutboundCommunicationListItemDto MapToListItemDto(OutboundCommunication c) => new()
    {
        Id = c.Id,
        EntityType = c.EntityType,
        EntityId = c.EntityId,
        ToAddress = c.ToAddress,
        ToName = c.ToName,
        FromAddress = c.FromAddress,
        Subject = c.Subject,
        Status = c.Status,
        SentAt = c.SentAt,
        CreatedByName = c.CreatedBy?.FullName ?? string.Empty,
        AttachmentCount = c.Attachments?.Count ?? 0,
        CreatedAt = c.CreatedAt,
    };

    private static OutboundCommunicationDto MapToDto(OutboundCommunication c) => new()
    {
        Id = c.Id,
        EntityType = c.EntityType,
        EntityId = c.EntityId,
        TemplateId = c.TemplateId,
        ToAddress = c.ToAddress,
        ToName = c.ToName,
        CcAddresses = c.CcAddresses,
        BccAddresses = c.BccAddresses,
        FromAddress = c.FromAddress,
        FromName = c.FromName,
        SenderType = c.SenderType,
        Subject = c.Subject,
        BodyHtml = c.BodyHtml,
        Status = c.Status,
        FailureReason = c.FailureReason,
        GraphMessageId = c.GraphMessageId,
        CreatedByName = c.CreatedBy?.FullName ?? string.Empty,
        SentByName = c.SentBy?.FullName,
        SentAt = c.SentAt,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        Attachments = c.Attachments?.Select(a => new OutboundCommunicationAttachmentDto
        {
            AttachmentId = a.AttachmentId,
            FileName = a.Attachment?.FileName ?? string.Empty,
        }).ToList() ?? [],
    };
}
