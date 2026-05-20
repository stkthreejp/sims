using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SIMS.Application.Services;

public class QuoteChecklistService : IQuoteChecklistService
{
    private readonly IServiceProvider _sp;

    private DbContext Db =>
        (DbContext)_sp.GetService(typeof(DbContext))!;

    public QuoteChecklistService(IServiceProvider sp)
    {
        _sp = sp;
    }

    // Auto-completable trigger keys and what to check
    private static readonly HashSet<string> AutoTriggers =
        ["rated", "auto_safety_refreshed", "has_application", "has_loss_runs", "has_mvr"];

    private static readonly HashSet<PolicyLineOfBusiness> AutoLobs =
        [PolicyLineOfBusiness.CommercialAuto, PolicyLineOfBusiness.AutoLiability, PolicyLineOfBusiness.AutoPhysicalDamage];

    // Default items seeded at quote creation — order matters (SortOrder = index)
    private static List<(string key, string label, bool isBlocker, bool autoLobOnly)> DefaultItems =>
    [
        ("rated",                 "Quote Rated",             true,  false),
        ("has_application",       "Signed Application",      true,  false),
        ("has_loss_runs",         "Loss Runs Received",      true,  false),
        ("auto_safety_refreshed", "Auto Safety Refreshed",   true,  true),
        ("has_mvr",               "MVR Reports",             false, true),
        ("coverage_confirmed",    "Coverage Terms Confirmed", true, false),
    ];

    public async Task SeedDefaultsAsync(Guid quoteId, PolicyLineOfBusiness lob)
    {
        var isAutoLob = AutoLobs.Contains(lob);
        var items = DefaultItems
            .Where(d => !d.autoLobOnly || isAutoLob)
            .Select((d, i) => new QuoteChecklistItem
            {
                QuoteId        = quoteId,
                TriggerKey     = d.key,
                Label          = d.label,
                IsBlocker      = d.isBlocker,
                SortOrder      = i,
            })
            .ToList();

        items.AddRange(await BuildPublishedGuidelineChecklistItemsAsync(quoteId, items.Count));

        Db.Set<QuoteChecklistItem>().AddRange(items);
        await Db.SaveChangesAsync();

        // Evaluate auto-triggers immediately so already-satisfied items are completed at creation
        var anyUpdated = await EvaluateTriggersAsync(quoteId, items);
        if (anyUpdated)
            await Db.SaveChangesAsync();
    }

    public async Task<Result<List<QuoteChecklistItemDto>>> GetForQuoteAsync(Guid quoteId)
    {
        var items = await Db.Set<QuoteChecklistItem>()
            .Where(c => c.QuoteId == quoteId && !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var addedItems = await AddMissingPublishedGuidelineItemsAsync(quoteId, items);
        if (addedItems.Count > 0)
            items.AddRange(addedItems);

        if (items.Count == 0)
            return Result<List<QuoteChecklistItemDto>>.Success([]);

        // Evaluate auto-triggers and complete any that now pass
        var anyUpdated = await EvaluateTriggersAsync(quoteId, items);
        if (anyUpdated)
            await Db.SaveChangesAsync();

        return Result<List<QuoteChecklistItemDto>>.Success(items.Select(Map).ToList());
    }

    public async Task<Result<QuoteChecklistItemDto>> ToggleAsync(
        Guid itemId, bool completed, Guid userId, string userName)
    {
        var item = await Db.Set<QuoteChecklistItem>()
            .FirstOrDefaultAsync(c => c.Id == itemId && !c.IsDeleted);

        if (item == null)
            return Result<QuoteChecklistItemDto>.Failure("NOT_FOUND", "Checklist item not found.");

        item.IsCompleted       = completed;
        item.CompletionSource  = "Manual";
        item.CompletedById     = completed ? userId : null;
        item.CompletedByName   = completed ? userName : null;
        item.CompletedAt       = completed ? DateTime.UtcNow : null;

        await Db.SaveChangesAsync();
        return Result<QuoteChecklistItemDto>.Success(Map(item));
    }

    // Returns true if any item was updated
    private async Task<bool> EvaluateTriggersAsync(Guid quoteId, List<QuoteChecklistItem> items)
    {
        var pending = items.Where(i => !i.IsCompleted && AutoTriggers.Contains(i.TriggerKey)).ToList();
        if (pending.Count == 0) return false;

        var keys = pending.Select(p => p.TriggerKey).ToHashSet();
        var now = DateTime.UtcNow;
        var anyUpdated = false;

        // rated
        if (keys.Contains("rated"))
        {
            var hasSnapshot = await Db.Set<QuoteRatingSnapshot>()
                .AnyAsync(r => r.QuoteId == quoteId && !r.IsDeleted);
            if (hasSnapshot)
                anyUpdated |= AutoComplete(pending, "rated", now);
        }

        // has_application / has_loss_runs / has_mvr — all attachment-based
        var attachmentKeys = new[] { "has_application", "has_loss_runs", "has_mvr" }
            .Where(k => keys.Contains(k)).ToList();

        if (attachmentKeys.Count > 0)
        {
            var attachments = await Db.Set<Attachment>()
                .Where(a => a.QuoteId == quoteId && !a.IsDeleted)
                .Select(a => a.FileName.ToLower())
                .ToListAsync();

            if (keys.Contains("has_application") && attachments.Any(f => f.Contains("application")))
                anyUpdated |= AutoComplete(pending, "has_application", now);

            if (keys.Contains("has_loss_runs") && attachments.Any(f => f.Contains("loss")))
                anyUpdated |= AutoComplete(pending, "has_loss_runs", now);

            if (keys.Contains("has_mvr") && attachments.Any(f => f.Contains("mvr")))
                anyUpdated |= AutoComplete(pending, "has_mvr", now);
        }

        // auto_safety_refreshed — check if FmcsaScoringRun exists for the insured's DOT
        if (keys.Contains("auto_safety_refreshed"))
        {
            var dotNumber = await Db.Set<Quote>()
                .Where(q => q.Id == quoteId)
                .Select(q => q.Submission.Insured.UsDotNumber)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(dotNumber))
            {
                var hasRun = await Db.Set<FmcsaScoringRun>()
                    .AnyAsync(r => r.UsDotNumber == dotNumber && !r.IsDeleted);
                if (hasRun)
                    anyUpdated |= AutoComplete(pending, "auto_safety_refreshed", now);
            }
        }

        return anyUpdated;
    }

    private async Task<List<QuoteChecklistItem>> AddMissingPublishedGuidelineItemsAsync(Guid quoteId, List<QuoteChecklistItem> existingItems)
    {
        var existingKeys = existingItems.Select(i => i.TriggerKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextSortOrder = existingItems.Count == 0 ? 0 : existingItems.Max(i => i.SortOrder) + 1;
        var missing = (await BuildPublishedGuidelineChecklistItemsAsync(quoteId, nextSortOrder))
            .Where(i => !existingKeys.Contains(i.TriggerKey))
            .ToList();

        if (missing.Count == 0)
            return [];

        Db.Set<QuoteChecklistItem>().AddRange(missing);
        await Db.SaveChangesAsync();
        return missing;
    }

    private async Task<List<QuoteChecklistItem>> BuildPublishedGuidelineChecklistItemsAsync(Guid quoteId, int startSortOrder)
    {
        var quote = await Db.Set<Quote>()
            .Include(q => q.Submission)
                .ThenInclude(s => s.Insured)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == quoteId && !q.IsDeleted);

        if (quote is null)
            return [];

        var state = string.IsNullOrWhiteSpace(quote.Submission.Insured.State)
            ? "ALL"
            : quote.Submission.Insured.State.Trim().ToUpperInvariant();

        var allowedStages = new[]
        {
            UnderwritingControlStage.Submission,
            UnderwritingControlStage.Quote,
            UnderwritingControlStage.Bind
        };

        var controls = await Db.Set<UnderwritingGuidelineControl>()
            .AsNoTracking()
            .Where(c => c.Status == UnderwritingControlStatus.Published
                && c.ItemType == UnderwritingControlItemType.DocumentChecklistItem
                && allowedStages.Contains(c.Stage)
                && c.LineOfBusiness == quote.LineOfBusiness
                && (c.CarrierId == null || c.CarrierId == quote.CarrierId)
                && (c.StateCode == "ALL" || c.StateCode == state))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Label)
            .ToListAsync();

        return controls.Select((control, index) => new QuoteChecklistItem
        {
            QuoteId = quoteId,
            TriggerKey = $"guideline:{control.Id}",
            Label = control.Label,
            IsBlocker = control.IsBlocking,
            SortOrder = startSortOrder + index,
        }).ToList();
    }

    private static bool AutoComplete(List<QuoteChecklistItem> items, string key, DateTime now)
    {
        var item = items.FirstOrDefault(i => i.TriggerKey == key);
        if (item == null || item.IsCompleted) return false;

        item.IsCompleted      = true;
        item.CompletionSource = "System";
        item.CompletedAt      = now;
        return true;
    }

    private static QuoteChecklistItemDto Map(QuoteChecklistItem i) => new(
        i.Id, i.QuoteId, i.TriggerKey, i.Label, i.IsBlocker, i.SortOrder,
        i.IsCompleted, i.CompletionSource, i.CompletedById, i.CompletedByName, i.CompletedAt);
}
