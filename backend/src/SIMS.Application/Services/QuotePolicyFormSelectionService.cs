using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class QuotePolicyFormSelectionService : IQuotePolicyFormSelectionService
{
    private readonly IServiceProvider _sp;

    public QuotePolicyFormSelectionService(IServiceProvider sp)
    {
        _sp = sp;
    }

    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public async Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> GetOrSeedAsync(Guid quoteId)
    {
        var existing = await GetSelectionsAsync(quoteId);
        if (existing.Count > 0)
            return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Success(existing);

        return await ResetFromPackageAsync(quoteId);
    }

    public async Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> SaveAsync(Guid quoteId, IReadOnlyList<QuotePolicyFormSelectionUpsertDto> forms)
    {
        var quoteExists = await Db.Set<Quote>().AnyAsync(q => q.Id == quoteId);
        if (!quoteExists)
            return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Failure("NOT_FOUND", "Quote not found.");

        if (forms.Any(f => f.PolicyFormTemplateId == Guid.Empty || f.SequenceOrder <= 0))
            return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Failure("VALIDATION", "Each form needs a template and sequence.");

        var templateIds = forms.Select(f => f.PolicyFormTemplateId).Distinct().ToList();
        var templateCount = await Db.Set<PolicyFormTemplate>().CountAsync(f => templateIds.Contains(f.Id));
        if (templateCount != templateIds.Count)
            return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Failure("VALIDATION", "One or more selected forms were not found.");

        var existing = await Db.Set<QuotePolicyFormSelection>().Where(f => f.QuoteId == quoteId).ToListAsync();
        Db.Set<QuotePolicyFormSelection>().RemoveRange(existing);

        foreach (var form in forms.OrderBy(f => f.SequenceOrder))
        {
            Db.Set<QuotePolicyFormSelection>().Add(new QuotePolicyFormSelection
            {
                QuoteId = quoteId,
                PolicyFormTemplateId = form.PolicyFormTemplateId,
                SequenceOrder = form.SequenceOrder,
                FormType = form.FormType,
                IsIncluded = form.IsIncluded,
                IsSystemGenerated = form.IsSystemGenerated,
                TriggerConditionJson = string.IsNullOrWhiteSpace(form.TriggerConditionJson) ? null : form.TriggerConditionJson.Trim(),
                Notes = form.Notes?.Trim(),
            });
        }

        await Db.SaveChangesAsync();
        return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Success(await GetSelectionsAsync(quoteId));
    }

    public async Task<Result<IReadOnlyList<QuotePolicyFormSelectionDto>>> ResetFromPackageAsync(Guid quoteId)
    {
        var quote = await Db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.AdditionalInterests)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Locations)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote == null)
            return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Failure("NOT_FOUND", "Quote not found.");

        var existing = await Db.Set<QuotePolicyFormSelection>().Where(f => f.QuoteId == quoteId).ToListAsync();
        Db.Set<QuotePolicyFormSelection>().RemoveRange(existing);

        var snapshot = await Db.Set<QuoteRatingSnapshot>()
            .Where(s => s.QuoteId == quoteId)
            .OrderByDescending(s => s.RatedAt)
            .FirstOrDefaultAsync();

        var state = ResolvePackageState(quote);
        var package = await Db.Set<PolicyPackageConfiguration>()
            .Include(p => p.Forms).ThenInclude(f => f.PolicyFormTemplate)
            .Where(p => p.IsActive
                && p.CarrierId == quote.CarrierId
                && p.LineOfBusiness == quote.LineOfBusiness
                && (p.State == state || p.State == null)
                && (p.ProgramConfigurationId == quote.ProgramId || p.ProgramConfigurationId == null))
            .OrderByDescending(p => p.ProgramConfigurationId == quote.ProgramId ? 1 : 0)
            .ThenByDescending(p => p.State == state ? 1 : 0)
            .ThenByDescending(p => p.UpdatedAt)
            .FirstOrDefaultAsync();

        if (package != null)
        {
            var sequence = 1;
            foreach (var form in package.Forms.OrderBy(f => f.SequenceOrder).Where(f => ShouldIncludePackageForm(f, quote, snapshot)))
            {
                Db.Set<QuotePolicyFormSelection>().Add(new QuotePolicyFormSelection
                {
                    QuoteId = quoteId,
                    PolicyFormTemplateId = form.PolicyFormTemplateId,
                    SequenceOrder = sequence++,
                    FormType = form.FormType,
                    IsIncluded = true,
                    IsSystemGenerated = true,
                    TriggerConditionJson = form.TriggerConditionJson,
                    Notes = form.Notes,
                });
            }
        }

        await Db.SaveChangesAsync();
        return Result<IReadOnlyList<QuotePolicyFormSelectionDto>>.Success(await GetSelectionsAsync(quoteId));
    }

    private async Task<IReadOnlyList<QuotePolicyFormSelectionDto>> GetSelectionsAsync(Guid quoteId)
    {
        return await Db.Set<QuotePolicyFormSelection>()
            .Include(f => f.PolicyFormTemplate)
            .Where(f => f.QuoteId == quoteId)
            .OrderBy(f => f.SequenceOrder)
            .Select(f => new QuotePolicyFormSelectionDto
            {
                Id = f.Id,
                QuoteId = f.QuoteId,
                PolicyFormTemplateId = f.PolicyFormTemplateId,
                FormNumber = f.PolicyFormTemplate.FormNumber,
                FormName = f.PolicyFormTemplate.Name,
                EditionDate = f.PolicyFormTemplate.EditionDate,
                SequenceOrder = f.SequenceOrder,
                FormType = f.FormType,
                IsIncluded = f.IsIncluded,
                IsSystemGenerated = f.IsSystemGenerated,
                TriggerConditionJson = f.TriggerConditionJson,
                Notes = f.Notes,
            })
            .ToListAsync();
    }

    private static bool ShouldIncludePackageForm(PolicyPackageForm packageForm, Quote quote, QuoteRatingSnapshot? snapshot)
        => packageForm.FormType switch
        {
            PolicyFormType.Mandatory => true,
            PolicyFormType.AdHoc => false,
            PolicyFormType.Conditional => EvaluateTriggerCondition(packageForm.TriggerConditionJson, quote, snapshot),
            _ => false,
        };

    private static bool EvaluateTriggerCondition(string? triggerConditionJson, Quote quote, QuoteRatingSnapshot? snapshot)
    {
        if (string.IsNullOrWhiteSpace(triggerConditionJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(triggerConditionJson);
            return EvaluateTriggerNode(doc.RootElement, quote, snapshot);
        }
        catch
        {
            return false;
        }
    }

    private static bool EvaluateTriggerNode(JsonElement node, Quote quote, QuoteRatingSnapshot? snapshot)
    {
        if (node.TryGetProperty("all", out var all) && all.ValueKind == JsonValueKind.Array)
            return all.EnumerateArray().All(child => EvaluateTriggerNode(child, quote, snapshot));

        if (node.TryGetProperty("any", out var any) && any.ValueKind == JsonValueKind.Array)
            return any.EnumerateArray().Any(child => EvaluateTriggerNode(child, quote, snapshot));

        if (!node.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String)
            return false;

        var actual = GetTriggerValue(pathElement.GetString(), quote, snapshot);
        if (actual == null)
            return false;

        if (node.TryGetProperty("equals", out var equals))
            return TriggerValuesEqual(actual, equals);

        if (node.TryGetProperty("notEquals", out var notEquals))
            return !TriggerValuesEqual(actual, notEquals);

        if (node.TryGetProperty("greaterThan", out var greaterThan) && TryGetDecimal(actual, out var actualDecimal) && TryGetDecimal(greaterThan, out var greaterThanDecimal))
            return actualDecimal > greaterThanDecimal;

        if (node.TryGetProperty("lessThan", out var lessThan) && TryGetDecimal(actual, out actualDecimal) && TryGetDecimal(lessThan, out var lessThanDecimal))
            return actualDecimal < lessThanDecimal;

        return false;
    }

    private static object? GetTriggerValue(string? path, Quote quote, QuoteRatingSnapshot? snapshot)
        => path switch
        {
            "Rating.DebrisRemoval" => snapshot?.DebrisRemoval,
            "Rating.RentalReimbursement" => snapshot?.RentalReimbursement,
            "Rating.TowingStorageRecovery" => snapshot?.TowingStorageRecovery,
            "Rating.NewlyAcquiredEquipment" => snapshot?.NewlyAcquiredEquipment,
            "Rating.Tria" => snapshot?.Tria,
            "Rating.EndorsementPremium" => snapshot?.EndorsementPremium,
            "Rating.GrandTotalPremium" => snapshot?.GrandTotalPremium,
            "Quote.TotalPremium" => quote.TotalPremium,
            "Quote.PremiumAmount" => quote.PremiumAmount,
            "Quote.IsFilingState" => quote.IsFilingState,
            "Quote.LineOfBusiness" => quote.LineOfBusiness.ToString(),
            "Submission.State" => ResolvePackageState(quote),
            "Submission.LossPayeeCount" => quote.Submission.AdditionalInterests.Count(i => !i.IsDeleted && i.LineOfBusiness == quote.LineOfBusiness && i.LossPayee),
            _ => null,
        };

    private static string ResolvePackageState(Quote quote)
        => (quote.Submission.Insured.State ?? ExtractState(quote.Submission.Locations.FirstOrDefault()?.Address) ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

    private static string? ExtractState(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return null;

        var stateZip = parts[^1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return stateZip.FirstOrDefault(p => p.Length == 2);
    }

    private static bool TriggerValuesEqual(object actual, JsonElement expected)
    {
        if (actual is bool boolValue && expected.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return boolValue == expected.GetBoolean();

        if (TryGetDecimal(actual, out var actualDecimal) && TryGetDecimal(expected, out var expectedDecimal))
            return actualDecimal == expectedDecimal;

        return string.Equals(Convert.ToString(actual), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetDecimal(object value, out decimal result)
    {
        if (value is JsonElement element)
            return TryGetDecimal(element, out result);

        if (value is decimal d)
        {
            result = d;
            return true;
        }

        return decimal.TryParse(Convert.ToString(value), out result);
    }

    private static bool TryGetDecimal(JsonElement value, out decimal result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out result))
            return true;
        if (value.ValueKind == JsonValueKind.String)
            return decimal.TryParse(value.GetString(), out result);
        result = default;
        return false;
    }
}
