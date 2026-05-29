using Azure.Identity;
using SIMS.Application.Configuration;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace SIMS.Infrastructure.Services;

public class EmailIngestionService : IEmailIngestionService
{
    private readonly ApplicationDbContext _db;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<EmailIngestionService> _logger;
    private readonly GraphServiceClient _graphClient;
    private readonly string _mailboxAddress;

    public EmailIngestionService(
        ApplicationDbContext db,
        IBlobStorageService blobStorage,
        IConfiguration config,
        ILogger<EmailIngestionService> logger)
    {
        _db = db;
        _blobStorage = blobStorage;
        _logger = logger;

        var tenantId = MicrosoftAuthConfiguration.GetTenantId(config);
        var clientId = MicrosoftAuthConfiguration.GetClientId(config);
        var clientSecret = config["GraphApi:ClientSecret"]
            ?? throw new InvalidOperationException("GraphApi:ClientSecret is not configured.");

        _mailboxAddress = config["GraphApi:MailboxAddress"]
            ?? throw new InvalidOperationException("GraphApi:MailboxAddress is not configured.");

        var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
        _graphClient = new GraphServiceClient(credential, ["https://graph.microsoft.com/.default"]);
    }

    public async Task IngestNewEmailsAsync(CancellationToken cancellationToken = default)
    {
        MessageCollectionResponse? response;

        try
        {
            response = await _graphClient
                .Users[_mailboxAddress]
                .Messages
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = "isRead eq false";
                    config.QueryParameters.Select = ["id", "from", "subject", "body", "receivedDateTime", "hasAttachments"];
                    config.QueryParameters.Top = 50;
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch messages from Graph API for mailbox {Mailbox}", _mailboxAddress);
            return;
        }

        var messages = response?.Value ?? [];

        foreach (var message in messages)
        {
            if (message.Id == null) continue;

            // Skip already-ingested messages (idempotency guard)
            if (await _db.Set<InboundEmail>().AnyAsync(e => e.GraphMessageId == message.Id, cancellationToken))
                continue;

            try
            {
                await IngestMessageAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ingest message {MessageId}", message.Id);
            }
        }
    }

    private async Task IngestMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var inboundEmail = new InboundEmail
        {
            GraphMessageId = message.Id,
            FromAddress = message.From?.EmailAddress?.Address ?? string.Empty,
            FromName = message.From?.EmailAddress?.Name,
            Subject = message.Subject ?? "(no subject)",
            BodyText = message.Body?.Content,
            ReceivedAt = message.ReceivedDateTime?.UtcDateTime ?? DateTime.UtcNow,
            IsProcessed = false,
        };

        _db.Set<InboundEmail>().Add(inboundEmail);

        if (message.HasAttachments == true)
        {
            AttachmentCollectionResponse? attachmentResponse = null;
            try
            {
                attachmentResponse = await _graphClient
                    .Users[_mailboxAddress]
                    .Messages[message.Id]
                    .Attachments
                    .GetAsync(cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch attachments for message {MessageId}", message.Id);
            }

            foreach (var attachment in attachmentResponse?.Value ?? [])
            {
                if (attachment is FileAttachment file && file.ContentBytes != null)
                {
                    var blobPath = await UploadAttachmentAsync(file);
                    if (blobPath == null) continue;

                    inboundEmail.Attachments.Add(new EmailAttachment
                    {
                        FileName = file.Name ?? "attachment",
                        ContentType = file.ContentType,
                        BlobUrl = blobPath,
                        FileSizeBytes = file.ContentBytes.Length,
                        DocumentType = DetectDocumentType(file.Name),
                    });
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Mark email as read in Graph
        try
        {
            await _graphClient
                .Users[_mailboxAddress]
                .Messages[message.Id]
                .PatchAsync(new Message { IsRead = true }, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not mark message {MessageId} as read", message.Id);
        }
    }

    private async Task<string?> UploadAttachmentAsync(FileAttachment file)
    {
        try
        {
            using var stream = new MemoryStream(file.ContentBytes!);
            return await _blobStorage.UploadAsync(stream, file.Name ?? "attachment", file.ContentType ?? "application/octet-stream");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload attachment {FileName} to blob storage", file.Name);
            return null;
        }
    }

    private static EmailAttachmentDocumentType DetectDocumentType(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return EmailAttachmentDocumentType.Unknown;
        var name = fileName.ToLowerInvariant();

        if (name.Contains("acord 125") || name.Contains("acord125") || name.Contains("125 ")) return EmailAttachmentDocumentType.Acord125;
        if (name.Contains("acord 126") || name.Contains("acord126") || name.Contains("126 ")) return EmailAttachmentDocumentType.Acord126;
        if (name.Contains("loss run") || name.Contains("lossrun") || name.Contains("loss_run")) return EmailAttachmentDocumentType.LossRun;
        if (name.Contains("dec page") || name.Contains("decpage") || name.Contains("declarations")) return EmailAttachmentDocumentType.DecPage;
        if (name.Contains("schedule of values") || name.Contains("sov")) return EmailAttachmentDocumentType.ScheduleOfValues;
        if (name.Contains("signed")) return EmailAttachmentDocumentType.SignedApplication;

        return EmailAttachmentDocumentType.Unknown;
    }
}
