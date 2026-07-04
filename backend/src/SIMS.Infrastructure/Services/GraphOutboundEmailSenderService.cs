using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using GraphAttachment = Microsoft.Graph.Models.Attachment;
using SimsAttachment = SIMS.Domain.Entities.Attachment;

namespace SIMS.Infrastructure.Services;

public class GraphOutboundEmailSenderService : IOutboundEmailSenderService
{
    private const long SimpleAttachmentLimitBytes = 3_000_000;

    private readonly DbContext _db;
    private readonly IBlobStorageService _blob;
    private readonly IConfiguration _config;

    public GraphOutboundEmailSenderService(DbContext db, IBlobStorageService blob, IConfiguration config)
    {
        _db = db;
        _blob = blob;
        _config = config;
    }

    public async Task<Result<OutboundEmailSendResult>> SendAsync(OutboundCommunication communication, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(communication.ToAddress))
            return Result<OutboundEmailSendResult>.Failure("MISSING_RECIPIENT", "The email draft does not have a recipient.");

        try
        {
            var (graphClient, mailboxAddress) = CreateGraphClient();

            // Non-production sink: when Email:RedirectAllTo is set, every send goes to
            // that address instead of the real recipients, with the original recipient
            // preserved in the subject.
            var redirectAllTo = _config["Email:RedirectAllTo"];
            var subject = communication.Subject;
            var toAddress = communication.ToAddress;
            var toName = communication.ToName;
            var ccAddresses = communication.CcAddresses;
            var bccAddresses = communication.BccAddresses;
            if (!string.IsNullOrWhiteSpace(redirectAllTo))
            {
                subject = $"[TEST → {communication.ToAddress}] {subject}";
                toAddress = redirectAllTo;
                toName = null;
                ccAddresses = null;
                bccAddresses = null;
            }

            var message = new Message
            {
                Subject = subject,
                Body = new ItemBody { ContentType = BodyType.Html, Content = communication.BodyHtml },
                ToRecipients = BuildRecipients(toAddress, toName),
                CcRecipients = BuildRecipients(ccAddresses, null),
                BccRecipients = BuildRecipients(bccAddresses, null),
                ReplyTo = BuildRecipients(communication.FromAddress, communication.FromName),
                Attachments = await BuildAttachmentsAsync(communication, cancellationToken),
            };

            var created = await graphClient.Users[mailboxAddress]
                .Messages
                .PostAsync(message, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(created?.Id))
                return Result<OutboundEmailSendResult>.Failure("GRAPH_MESSAGE_NOT_CREATED", "Microsoft Graph did not return a message id.");

            await graphClient.Users[mailboxAddress]
                .Messages[created.Id]
                .Send
                .PostAsync(cancellationToken: cancellationToken);

            return Result<OutboundEmailSendResult>.Success(new OutboundEmailSendResult(created.Id, created.WebLink));
        }
        catch (Exception ex)
        {
            return Result<OutboundEmailSendResult>.Failure("GRAPH_SEND_FAILED", ex.Message);
        }
    }

    private (GraphServiceClient GraphClient, string MailboxAddress) CreateGraphClient()
    {
        var tenantId = GetRequiredConfig("GraphApi:TenantId", "MicrosoftAuth:TenantId", "AzureAd:TenantId");
        var clientId = GetRequiredConfig("GraphApi:ClientId", "MicrosoftAuth:ClientId", "AzureAd:ClientId");
        var clientSecret = GetRequiredConfig("GraphApi:ClientSecret");

        var mailboxAddress = GetRequiredConfig("GraphApi:MailboxAddress");

        var graphClient = new GraphServiceClient(
            new ClientSecretCredential(tenantId, clientId, clientSecret),
            ["https://graph.microsoft.com/.default"]);

        return (graphClient, mailboxAddress);
    }

    private string GetRequiredConfig(params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = _config[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        throw new InvalidOperationException($"{string.Join(" or ", keys)} is not configured.");
    }

    private async Task<List<GraphAttachment>?> BuildAttachmentsAsync(
        OutboundCommunication communication,
        CancellationToken cancellationToken)
    {
        if (communication.Attachments.Count == 0)
            return null;

        var attachmentIds = communication.Attachments
            .Where(a => !a.IsDeleted)
            .Select(a => a.AttachmentId)
            .Distinct()
            .ToList();

        if (attachmentIds.Count == 0)
            return null;

        var attachments = await _db.Set<SimsAttachment>()
            .AsNoTracking()
            .Where(a => attachmentIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var graphAttachments = new List<GraphAttachment>();
        foreach (var attachment in attachments)
        {
            if (attachment.FileSizeBytes > SimpleAttachmentLimitBytes)
                throw new InvalidOperationException($"{attachment.FileName} is too large to send as a simple email attachment.");

            graphAttachments.Add(new FileAttachment
            {
                OdataType = "#microsoft.graph.fileAttachment",
                Name = attachment.FileName,
                ContentType = attachment.ContentType,
                ContentBytes = await _blob.DownloadAsync(attachment.BlobPath),
            });
        }

        return graphAttachments;
    }

    private static List<Recipient> BuildRecipients(string? addresses, string? displayName)
    {
        return SplitAddresses(addresses)
            .Select((address, index) => new Recipient
            {
                EmailAddress = new EmailAddress
                {
                    Address = address,
                    Name = index == 0 ? displayName : null,
                }
            })
            .ToList();
    }

    private static IEnumerable<string> SplitAddresses(string? addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses))
            return [];

        return addresses
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !string.IsNullOrWhiteSpace(a));
    }
}
