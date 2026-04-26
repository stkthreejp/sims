using IMS.Application.Common;
using IMS.Application.DTOs.InboundEmails;
using IMS.Application.DTOs.Submissions;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class InboundEmailService : IInboundEmailService
{
    private readonly IServiceProvider _sp;
    private Microsoft.EntityFrameworkCore.DbContext Db =>
        (Microsoft.EntityFrameworkCore.DbContext)_sp.GetService(typeof(Microsoft.EntityFrameworkCore.DbContext))!;

    public InboundEmailService(IServiceProvider sp) => _sp = sp;

    public async Task<IEnumerable<InboundEmailListItemDto>> GetUnprocessedAsync()
    {
        var emails = await Db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .Where(e => !e.IsDeleted && !e.IsProcessed)
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();

        return emails.Select(MapToListItemDto);
    }

    public async Task<Result<InboundEmailDto>> GetByIdAsync(Guid id)
    {
        var email = await Db.Set<InboundEmail>()
            .Include(e => e.Attachments.Where(a => !a.IsDeleted))
            .FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);

        return email == null
            ? Result<InboundEmailDto>.Failure("NOT_FOUND", "Inbound email not found.")
            : Result<InboundEmailDto>.Success(MapToDto(email));
    }

    public async Task<Result<SubmissionDto>> CreateSubmissionFromEmailAsync(Guid emailId, Guid currentUserId)
    {
        var email = await Db.Set<InboundEmail>()
            .FirstOrDefaultAsync(e => e.Id == emailId && !e.IsDeleted);

        if (email == null)
            return Result<SubmissionDto>.Failure("NOT_FOUND", "Inbound email not found.");

        if (email.IsProcessed && email.LinkedSubmissionId.HasValue)
            return Result<SubmissionDto>.Failure("ALREADY_PROCESSED", "A submission has already been created from this email.");

        // Create a placeholder Insured from sender info
        var insured = BuildInsuredFromSender(email.FromName, email.FromAddress, currentUserId);
        Db.Set<Insured>().Add(insured);
        await Db.SaveChangesAsync();

        // Generate submission number
        var year = DateTime.UtcNow.Year;
        var prefix = $"SUB-{year}-";
        var count = await Db.Set<Submission>()
            .IgnoreQueryFilters()
            .CountAsync(s => s.SubmissionNumber.StartsWith(prefix));
        var submissionNumber = $"{prefix}{(count + 1):D4}";

        var submission = new Submission
        {
            SubmissionNumber = submissionNumber,
            InsuredId = insured.Id,
            UnderwriterId = currentUserId,
            CreatedById = currentUserId,
            Status = SubmissionStatus.New,
        };
        Db.Set<Submission>().Add(submission);

        // Link and mark email as processed
        email.LinkedSubmissionId = submission.Id;
        email.IsProcessed = true;
        email.ProcessedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync();

        await Db.Entry(submission).Reference(s => s.Insured).LoadAsync();
        await Db.Entry(submission).Reference(s => s.Underwriter).LoadAsync();

        return Result<SubmissionDto>.Success(new SubmissionDto
        {
            Id = submission.Id,
            SubmissionNumber = submission.SubmissionNumber,
            InsuredId = insured.Id,
            InsuredName = insured.DisplayName,
            UnderwriterId = currentUserId,
            UnderwriterName = submission.Underwriter?.FullName ?? "",
            Status = submission.Status,
            CreatedAt = submission.CreatedAt,
        });
    }

    private static Insured BuildInsuredFromSender(string? fromName, string fromAddress, Guid createdById)
    {
        // Treat as Individual; use email as placeholder address fields
        var parts = (fromName ?? fromAddress).Trim().Split(' ', 2);
        return new Insured
        {
            InsuredType = InsuredType.Individual,
            FirstName = parts[0],
            LastName = parts.Length > 1 ? parts[1] : string.Empty,
            Email = fromAddress,
            AddressLine1 = "Unknown",
            City = "Unknown",
            State = "XX",
            ZipCode = "00000",
            IsActive = true,
            CreatedById = createdById,
        };
    }

    private static InboundEmailListItemDto MapToListItemDto(InboundEmail e) => new()
    {
        Id = e.Id,
        FromAddress = e.FromAddress,
        FromName = e.FromName,
        Subject = e.Subject,
        ReceivedAt = e.ReceivedAt,
        IsProcessed = e.IsProcessed,
        LinkedSubmissionId = e.LinkedSubmissionId,
        AttachmentCount = e.Attachments?.Count ?? 0,
        CreatedAt = e.CreatedAt,
    };

    private static InboundEmailDto MapToDto(InboundEmail e) => new()
    {
        Id = e.Id,
        FromAddress = e.FromAddress,
        FromName = e.FromName,
        Subject = e.Subject,
        BodyText = e.BodyText,
        ReceivedAt = e.ReceivedAt,
        ProcessedAt = e.ProcessedAt,
        IsProcessed = e.IsProcessed,
        LinkedSubmissionId = e.LinkedSubmissionId,
        CreatedAt = e.CreatedAt,
        Attachments = e.Attachments?.Select(a => new EmailAttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            BlobUrl = a.BlobUrl,
            FileSizeBytes = a.FileSizeBytes,
            DocumentType = a.DocumentType,
        }).ToList() ?? [],
    };
}
