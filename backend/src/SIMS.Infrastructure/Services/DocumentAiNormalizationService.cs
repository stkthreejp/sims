using System.Globalization;
using System.Text.RegularExpressions;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Application.DTOs.Submissions;
using SIMS.Domain.Enums;

namespace SIMS.Infrastructure.Services;

public static partial class DocumentAiNormalizationService
{
    public static DocumentAiNormalizationPreview Normalize(DocumentAiExtractionResult extraction)
    {
        var preview = new DocumentAiNormalizationPreview
        {
            FieldsRequiringReview = extraction.Fields.Where(f => f.RequiresReview).ToList()
        };

        MapSubmissionFields(preview.SubmissionData, extraction.Fields);
        preview.LossYears.AddRange(MapLossYears(extraction.Fields));

        if (preview.LossYears.Count > 0)
            preview.Warnings.Add("Loss run preview only. Review extracted totals before importing.");

        return preview;
    }

    private static void MapSubmissionFields(DocumentExtractionResult target, List<DocumentAiExtractedField> fields)
    {
        target.DescriptionOfOperations = FirstValue(fields,
            "DESCRIPTION OF PRIMARY OPERATIONS",
            "DESCRIPTION OF OPERATIONS");

        target.Dba = FirstValue(fields, "DBA", "DBA NAME", "DOING BUSINESS AS");
        target.EntityType = FirstCheckedEntityType(fields);

        if (IsChecked(fields, "COMMERCIAL INLAND MARINE"))
            target.IMCoverages = new ExtractedIMCoverages();
    }

    private static IEnumerable<SubmissionLossYearCreateDto> MapLossYears(List<DocumentAiExtractedField> fields)
    {
        return fields
            .GroupBy(f => f.PageNumber)
            .Select(group => MapLossYear(group.ToList()))
            .Where(year => year != null)
            .OrderBy(year => year!.PolicyYear)
            .Select(year => year!);
    }

    private static SubmissionLossYearCreateDto? MapLossYear(List<DocumentAiExtractedField> pageFields)
    {
        var term = FirstValue(pageFields, "TERM");
        var policyYear = ParsePolicyYear(term);
        if (policyYear == null)
            return null;

        var totals = ParseMoneyValues(FirstValue(pageFields, "TOTALS"));
        var carrierField = pageFields.FirstOrDefault(f =>
            !IsKnownLossRunLabel(f.Name) && LooksLikePolicyNumber(f.Value));

        return new SubmissionLossYearCreateDto
        {
            PolicyYear = policyYear.Value,
            LineOfBusiness = FirstValue(pageFields, "LINE OF BUSINESS"),
            CarrierName = Clean(carrierField?.Name),
            PolicyNumber = Clean(carrierField?.Value),
            PremiumAmount = 0,
            PremiumBasis = LossPremiumBasis.Actual,
            IsSmmWritten = false,
            Source = "DocumentAI",
            AsOfDate = ParseDate(FirstValue(pageFields, "AS OF")),
            PaidOverride = FirstMoney(pageFields, "PAID") ?? totals.ElementAtOrDefault(0),
            ReservedOverride = FirstMoney(pageFields, "RESERVE") ?? totals.ElementAtOrDefault(1),
            ExpenseOverride = FirstMoney(pageFields, "EXPENSE") ?? totals.ElementAtOrDefault(2),
            Notes = "AI extracted preview; requires user review before import."
        };
    }

    private static string? FirstCheckedEntityType(List<DocumentAiExtractedField> fields)
    {
        var entityTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["INDIVIDUAL"] = "Individual",
            ["SOLE PROPRIETOR"] = "SoleProprietor",
            ["PARTNERSHIP"] = "Partnership",
            ["LLC"] = "LLC",
            ["CORPORATION"] = "Corporation",
            ["TRUST"] = "Trust"
        };

        foreach (var (fieldName, entityType) in entityTypes)
        {
            if (IsChecked(fields, fieldName))
                return entityType;
        }

        return null;
    }

    private static bool IsChecked(List<DocumentAiExtractedField> fields, string fieldName)
    {
        var field = BestField(fields, fieldName);
        if (field == null)
            return false;

        var value = field.Value.Trim();
        return value.Contains('\u2611')
            || value.Contains('\u2713')
            || value.Contains('X', StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FirstValue(List<DocumentAiExtractedField> fields, params string[] names)
    {
        foreach (var name in names)
        {
            var field = BestField(fields, name);
            var value = Clean(field?.Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static DocumentAiExtractedField? BestField(List<DocumentAiExtractedField> fields, string name)
    {
        var normalized = NormalizeName(name);
        return fields
            .Where(f => NormalizeName(f.Name) == normalized)
            .OrderByDescending(f => f.Confidence)
            .FirstOrDefault()
            ?? fields
                .Where(f => NormalizeName(f.Name).Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.Confidence)
                .FirstOrDefault();
    }

    private static int? ParsePolicyYear(string? term)
    {
        var date = DateRangeRegex().Match(term ?? string.Empty);
        if (!date.Success)
            return null;

        return int.TryParse(date.Groups["year"].Value, out var year) ? year : null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        return null;
    }

    private static decimal? FirstMoney(List<DocumentAiExtractedField> fields, string name)
    {
        var values = ParseMoneyValues(FirstValue(fields, name));
        return values.Count > 0 ? values[0] : null;
    }

    private static List<decimal> ParseMoneyValues(string? value)
    {
        return MoneyRegex()
            .Matches(value ?? string.Empty)
            .Select(m => decimal.Parse(m.Value.Replace("$", string.Empty).Replace(",", string.Empty), CultureInfo.InvariantCulture))
            .ToList();
    }

    private static bool LooksLikePolicyNumber(string? value) =>
        !string.IsNullOrWhiteSpace(value) && PolicyNumberRegex().IsMatch(value.Trim());

    private static bool IsKnownLossRunLabel(string value)
    {
        var normalized = NormalizeName(value);
        return normalized is "POLICY TYPE" or "LINE OF BUSINESS" or "AS OF" or "TERM" or "TOTALS" or "INCURRED" or "RESERVE" or "EXPENSE";
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim(':').Trim();

    private static string NormalizeName(string value) =>
        NameCleanupRegex().Replace(value, " ").Trim().ToUpperInvariant();

    [GeneratedRegex(@"[^\w]+")]
    private static partial Regex NameCleanupRegex();

    [GeneratedRegex(@"^\d{1,2}/\d{1,2}/(?<year>\d{4})\s*-")]
    private static partial Regex DateRangeRegex();

    [GeneratedRegex(@"\$?\d[\d,]*\.\d{2}")]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"^[A-Z]{2,}\d{5,}$", RegexOptions.IgnoreCase)]
    private static partial Regex PolicyNumberRegex();
}
