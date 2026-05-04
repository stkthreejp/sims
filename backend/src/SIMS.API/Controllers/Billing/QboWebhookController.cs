using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Infrastructure.Data;

namespace SIMS.API.Controllers.Billing;

[ApiController]
[Route("api/v1/billing/qbo/webhook")]
public class QboWebhookController : ControllerBase
{
    private readonly QboSettings _settings;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<QboWebhookController> _logger;

    public QboWebhookController(
        IOptions<QboSettings> settings,
        ApplicationDbContext db,
        ILogger<QboWebhookController> logger)
    {
        _settings = settings.Value;
        _db = db;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        // Read body for signature verification
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        if (!VerifySignature(body, Request.Headers["intuit-signature"].FirstOrDefault()))
        {
            _logger.LogWarning("QBO webhook received with invalid HMAC signature");
            return Unauthorized();
        }

        _logger.LogDebug("QBO webhook received: {Body}", body);

        try
        {
            var payload = JsonNode.Parse(body);
            var entities = payload?["eventNotifications"]?.AsArray();
            if (entities == null) return Ok();

            foreach (var notification in entities)
            {
                var dataChangeEvent = notification?["dataChangeEvent"];
                if (dataChangeEvent == null) continue;

                // Mark any JournalEntry changes as potentially divergent
                var jeEntities = dataChangeEvent["entities"]?.AsArray()
                    ?.Where(e => e?["name"]?.GetValue<string>() == "JournalEntry")
                    .ToList();

                if (jeEntities?.Count > 0)
                {
                    await MarkDivergentRollupsAsync(jeEntities, ct);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse QBO webhook payload");
        }

        return Ok();
    }

    private async Task MarkDivergentRollupsAsync(List<JsonNode?> jeChanges, CancellationToken ct)
    {
        var qboIds = jeChanges
            .Select(e => e?["id"]?.GetValue<string>())
            .Where(id => id != null)
            .ToList();

        if (qboIds.Count == 0) return;

        // Find rollups whose externalId contains any of these QBO JE IDs
        var affectedRollups = await _db.JournalEntryRollups
            .Where(r => r.DriverType == "QBO" && r.ExternalId != null)
            .ToListAsync(ct);

        var divergent = affectedRollups
            .Where(r => qboIds.Any(id => r.ExternalId!.Contains(id!)))
            .ToList();

        foreach (var rollup in divergent)
        {
            rollup.Status = "Divergent";
            _logger.LogWarning("QBO webhook: rollup {RollupId} marked divergent (QBO-side change detected)", rollup.Id);
        }

        if (divergent.Count > 0)
            await _db.SaveChangesAsync(ct);
    }

    private bool VerifySignature(string body, string? incomingSignature)
    {
        if (string.IsNullOrEmpty(incomingSignature))
            return false;

        if (string.IsNullOrEmpty(_settings.WebhookVerifierToken) ||
            _settings.WebhookVerifierToken == "PLACEHOLDER")
        {
            _logger.LogError("QBO webhook received but verifier token is not configured — rejecting");
            return false;
        }

        var key = Encoding.UTF8.GetBytes(_settings.WebhookVerifierToken);
        using var hmac = new HMACSHA256(key);
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
        return string.Equals(computed, incomingSignature, StringComparison.Ordinal);
    }
}
