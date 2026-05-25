using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Entities.Bordereaux;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class BordereauxService : IBordereauxService
{
    private readonly DbContext _db;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BordereauxService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<BordereauxProfileDto>> GetProfilesAsync(
        bool includeInactive = false,
        Guid? programId = null,
        Guid? carrierId = null,
        BordereauxReportType? reportType = null,
        BordereauxOutputFormat? outputFormat = null,
        CancellationToken ct = default)
    {
        var query = BaseProfileQuery();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        if (programId.HasValue)
            query = query.Where(p => p.ProgramConfigurationId == programId.Value);
        if (carrierId.HasValue)
            query = query.Where(p => p.CarrierId == carrierId.Value);
        if (reportType.HasValue)
            query = query.Where(p => p.ReportType == reportType.Value);
        if (outputFormat.HasValue)
            query = query.Where(p => p.OutputFormat == outputFormat.Value);

        var profiles = await query
            .OrderBy(p => p.ProgramConfiguration.Name)
            .ThenBy(p => p.Carrier.Name)
            .ThenBy(p => p.ReportType)
            .ThenBy(p => p.LineOfBusiness)
            .ThenBy(p => p.StateCode)
            .ToListAsync(ct);

        return profiles.Select(Map).ToList();
    }

    public async Task<Result<BordereauxProfileDto>> GetProfileAsync(Guid id, CancellationToken ct = default)
    {
        var profile = await BaseProfileQuery().FirstOrDefaultAsync(p => p.Id == id, ct);
        return profile is null
            ? Result<BordereauxProfileDto>.Failure("NOT_FOUND", "Bordereaux profile not found.")
            : Result<BordereauxProfileDto>.Success(Map(profile));
    }

    public async Task<Result<BordereauxProfileDto>> CreateProfileAsync(UpsertBordereauxProfileRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, null, ct);
        if (validation is not null)
            return Result<BordereauxProfileDto>.Failure(validation.Value.Code, validation.Value.Message);

        var profile = new BordereauxProfile();
        Apply(profile, request);
        _db.Set<BordereauxProfile>().Add(profile);
        await _db.SaveChangesAsync(ct);

        return Result<BordereauxProfileDto>.Success(await LoadProfileDtoAsync(profile.Id, ct));
    }

    public async Task<Result<BordereauxProfileDto>> UpdateProfileAsync(Guid id, UpsertBordereauxProfileRequest request, CancellationToken ct = default)
    {
        var profile = await _db.Set<BordereauxProfile>()
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
        if (profile is null)
            return Result<BordereauxProfileDto>.Failure("NOT_FOUND", "Bordereaux profile not found.");

        var validation = await ValidateAsync(request, id, ct);
        if (validation is not null)
            return Result<BordereauxProfileDto>.Failure(validation.Value.Code, validation.Value.Message);

        Apply(profile, request);
        profile.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Result<BordereauxProfileDto>.Success(await LoadProfileDtoAsync(profile.Id, ct));
    }

    public async Task<Result<BordereauxPremiumPreviewDto>> GetPremiumPreviewAsync(Guid profileId, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default)
    {
        if (periodEnd < periodStart)
            return Result<BordereauxPremiumPreviewDto>.Failure("INVALID_PERIOD", "Period end cannot be before period start.");

        var profile = await BaseProfileQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == profileId, ct);
        if (profile is null)
            return Result<BordereauxPremiumPreviewDto>.Failure("PROFILE_NOT_FOUND", "Bordereaux profile not found.");
        if (profile.ReportType != BordereauxReportType.Premium)
            return Result<BordereauxPremiumPreviewDto>.Failure("INVALID_REPORT_TYPE", "Only premium profiles support premium preview.");

        var includedTypes = ParseIncludedTransactionTypes(profile.IncludedTransactionTypesJson);
        var rows = await (
            from invoice in _db.Set<Invoice>().AsNoTracking().Include(i => i.Lines)
            join transaction in _db.Set<PolicyTransaction>().AsNoTracking()
                    .Include(t => t.Policy).ThenInclude(p => p.Program)
                    .Include(t => t.Policy).ThenInclude(p => p.Carrier)
                    .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Insured)
                on invoice.PolicyTransactionId equals transaction.Id
            where invoice.PolicyTransactionId != null
                && (invoice.Status == "Posted"
                    || invoice.Status == "PartiallyPaid"
                    || invoice.Status == "Paid")
                && (transaction.Status == PolicyTransactionStatus.Completed
                    || transaction.Status == PolicyTransactionStatus.Issued
                    || transaction.Status == PolicyTransactionStatus.Bound)
                && transaction.Policy.ProgramId == profile.ProgramConfigurationId
                && transaction.Policy.CarrierId == profile.CarrierId
                && (profile.LineOfBusiness == null || transaction.Policy.LineOfBusiness == profile.LineOfBusiness)
                && (profile.StateCode == null || transaction.Policy.Submission.Insured.State.ToUpper() == profile.StateCode)
                && (includedTypes.Count == 0 || includedTypes.Contains(transaction.TransactionType))
            select new PreviewSourceRow(invoice, transaction)
        ).ToListAsync(ct);

        var previewRows = rows
            .Select(row => BuildPreviewRow(row.Invoice, row.Transaction, profile))
            .Where(row => row.ReportingDate >= periodStart && row.ReportingDate <= periodEnd)
            .OrderBy(row => row.ReportingDate)
            .ThenBy(row => row.PolicyNumber)
            .ThenBy(row => row.TransactionNumber)
            .ToList();

        return Result<BordereauxPremiumPreviewDto>.Success(new BordereauxPremiumPreviewDto(
            profile.Id,
            periodStart,
            periodEnd,
            previewRows,
            previewRows.Sum(r => r.GrossPremium),
            previewRows.Sum(r => r.GrossCommission),
            previewRows.Sum(r => r.Fees),
            previewRows.Sum(r => r.NetDueCarrier)));
    }

    public async Task<Result<BordereauxRunDto>> CreatePremiumRunSnapshotAsync(
        Guid profileId,
        DateOnly periodStart,
        DateOnly periodEnd,
        Guid? generatedById,
        CancellationToken ct = default)
    {
        var preview = await GetPremiumPreviewAsync(profileId, periodStart, periodEnd, ct);
        if (!preview.IsSuccess)
            return Result<BordereauxRunDto>.Failure(preview.ErrorCode!, preview.ErrorMessage!);

        var profile = await BaseProfileQuery()
            .AsNoTracking()
            .SingleAsync(p => p.Id == profileId, ct);
        var nextRunNumber = await _db.Set<BordereauxRun>()
            .Where(r => r.BordereauxProfileId == profileId
                && r.PeriodStart == periodStart
                && r.PeriodEnd == periodEnd)
            .Select(r => (int?)r.RunNumber)
            .MaxAsync(ct) ?? 0;

        var rowCount = preview.Value!.Rows.Count;
        var run = new BordereauxRun
        {
            BordereauxProfileId = profileId,
            RunNumber = nextRunNumber + 1,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Status = BordereauxRunStatus.Draft,
            ReconciliationStatus = BordereauxReconciliationStatus.NotRun,
            GeneratedById = generatedById,
            BordereauxRowCount = rowCount,
            AccountCurrentRowCount = profile.RequiresAccountCurrent ? rowCount : 0,
            DetailRowCountsJson = JsonSerializer.Serialize(new
            {
                premiumRows = rowCount,
            }, SnapshotJsonOptions),
            ValidationSummaryJson = JsonSerializer.Serialize(new
            {
                status = "not_run",
                errors = 0,
                warnings = 0,
            }, SnapshotJsonOptions),
            ReconciliationSummaryJson = JsonSerializer.Serialize(new
            {
                status = "not_run",
                preview.Value.GrossPremiumTotal,
                preview.Value.GrossCommissionTotal,
                preview.Value.FeesTotal,
                preview.Value.NetDueCarrierTotal,
            }, SnapshotJsonOptions),
            ProfileSnapshotJson = JsonSerializer.Serialize(Map(profile), SnapshotJsonOptions),
            SourceRowsSnapshotJson = JsonSerializer.Serialize(preview.Value.Rows, SnapshotJsonOptions),
        };

        _db.Set<BordereauxRun>().Add(run);
        await _db.SaveChangesAsync(ct);

        return Result<BordereauxRunDto>.Success(MapRun(run, profile.Name));
    }

    private IQueryable<BordereauxProfile> BaseProfileQuery()
        => _db.Set<BordereauxProfile>()
            .Where(p => !p.IsDeleted)
            .Include(p => p.ProgramConfiguration)
            .Include(p => p.Carrier);

    private async Task<BordereauxProfileDto> LoadProfileDtoAsync(Guid id, CancellationToken ct)
        => Map(await BaseProfileQuery().SingleAsync(p => p.Id == id, ct));

    private async Task<(string Code, string Message)?> ValidateAsync(
        UpsertBordereauxProfileRequest request,
        Guid? existingProfileId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("NAME_REQUIRED", "Profile name is required.");

        if (!Enum.IsDefined(request.ReportType))
            return ("INVALID_REPORT_TYPE", "Report type is invalid.");
        if (!Enum.IsDefined(request.Frequency))
            return ("INVALID_FREQUENCY", "Frequency is invalid.");
        if (!Enum.IsDefined(request.OutputFormat))
            return ("INVALID_OUTPUT_FORMAT", "Output format is invalid.");
        if (!Enum.IsDefined(request.DateBasis))
            return ("INVALID_DATE_BASIS", "Date basis is invalid.");

        var stateCode = NormalizeState(request.StateCode);
        if (!string.IsNullOrWhiteSpace(request.StateCode) && stateCode is null)
            return ("INVALID_STATE", "State must be a two-character code.");

        var programExists = await _db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Id == request.ProgramConfigurationId && p.IsActive && !p.IsDeleted, ct);
        if (!programExists)
            return ("PROGRAM_NOT_FOUND", "Program not found or inactive.");

        var carrierExists = await _db.Set<Carrier>()
            .AnyAsync(c => c.Id == request.CarrierId && c.IsActive && !c.IsDeleted, ct);
        if (!carrierExists)
            return ("CARRIER_NOT_FOUND", "Carrier not found or inactive.");

        var jsonValidation = ValidateJsonArray(request.RequiredTabsJson, "REQUIRED_TABS");
        if (jsonValidation is not null)
            return jsonValidation;
        jsonValidation = ValidateJsonArray(request.RequiredColumnsJson, "REQUIRED_COLUMNS");
        if (jsonValidation is not null)
            return jsonValidation;
        jsonValidation = ValidateJsonObject(request.MappingRulesJson, "MAPPING_RULES");
        if (jsonValidation is not null)
            return jsonValidation;
        jsonValidation = ValidateJsonObject(request.StaticValuesJson, "STATIC_VALUES");
        if (jsonValidation is not null)
            return jsonValidation;
        jsonValidation = ValidateJsonObject(request.ValidationRulesJson, "VALIDATION_RULES");
        if (jsonValidation is not null)
            return jsonValidation;
        jsonValidation = ValidateJsonArray(request.IncludedTransactionTypesJson, "INCLUDED_TRANSACTION_TYPES");
        if (jsonValidation is not null)
            return jsonValidation;

        var duplicate = await _db.Set<BordereauxProfile>()
            .AnyAsync(p => !p.IsDeleted
                && p.IsActive
                && p.Id != existingProfileId
                && p.ProgramConfigurationId == request.ProgramConfigurationId
                && p.CarrierId == request.CarrierId
                && p.ReportType == request.ReportType
                && p.LineOfBusiness == request.LineOfBusiness
                && p.StateCode == stateCode, ct);
        if (request.IsActive && duplicate)
            return ("DUPLICATE_ACTIVE_PROFILE", "An active bordereaux profile already exists for this program, carrier, report type, LOB, and state.");

        return null;
    }

    private static void Apply(BordereauxProfile profile, UpsertBordereauxProfileRequest request)
    {
        profile.Name = request.Name.Trim();
        profile.ProgramConfigurationId = request.ProgramConfigurationId;
        profile.CarrierId = request.CarrierId;
        profile.LineOfBusiness = request.LineOfBusiness;
        profile.StateCode = NormalizeState(request.StateCode);
        profile.ReportType = request.ReportType;
        profile.Frequency = request.Frequency;
        profile.OutputFormat = request.OutputFormat;
        profile.DateBasis = request.DateBasis;
        profile.RequiresAccountCurrent = request.RequiresAccountCurrent;
        profile.IsActive = request.IsActive;
        profile.RequiredTabsJson = NormalizeJson(request.RequiredTabsJson);
        profile.RequiredColumnsJson = NormalizeJson(request.RequiredColumnsJson);
        profile.MappingRulesJson = NormalizeJson(request.MappingRulesJson);
        profile.StaticValuesJson = NormalizeJson(request.StaticValuesJson);
        profile.ValidationRulesJson = NormalizeJson(request.ValidationRulesJson);
        profile.IncludedTransactionTypesJson = NormalizeJson(request.IncludedTransactionTypesJson);
        profile.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
    }

    private static BordereauxProfileDto Map(BordereauxProfile p) => new(
        p.Id,
        p.Name,
        p.ProgramConfigurationId,
        p.ProgramConfiguration.Name,
        p.CarrierId,
        p.Carrier.Name,
        p.LineOfBusiness,
        p.StateCode,
        p.ReportType,
        p.Frequency,
        p.OutputFormat,
        p.DateBasis,
        p.RequiresAccountCurrent,
        p.IsActive,
        p.RequiredTabsJson,
        p.RequiredColumnsJson,
        p.MappingRulesJson,
        p.StaticValuesJson,
        p.ValidationRulesJson,
        p.IncludedTransactionTypesJson,
        p.Notes);

    private static BordereauxRunDto MapRun(BordereauxRun run, string profileName) => new(
        run.Id,
        run.BordereauxProfileId,
        profileName,
        run.RunNumber,
        run.PeriodStart,
        run.PeriodEnd,
        run.Status,
        run.ReconciliationStatus,
        run.GeneratedById,
        run.GeneratedAt,
        run.BordereauxRowCount,
        run.AccountCurrentRowCount,
        run.DetailRowCountsJson,
        run.ValidationSummaryJson,
        run.ReconciliationSummaryJson,
        run.ProfileSnapshotJson,
        run.SourceRowsSnapshotJson);

    private static BordereauxPremiumPreviewRowDto BuildPreviewRow(Invoice invoice, PolicyTransaction transaction, BordereauxProfile profile)
    {
        var reportingDate = ResolveReportingDate(transaction, invoice, profile.DateBasis);
        var insured = transaction.Policy.Submission.Insured;
        var grossCommission = invoice.CommissionAmount;

        return new BordereauxPremiumPreviewRowDto(
            transaction.PolicyId,
            transaction.Id,
            invoice.Id,
            transaction.Policy.PolicyNumber,
            transaction.TransactionNumber,
            transaction.TransactionType,
            reportingDate,
            transaction.EffectiveDate,
            invoice.InvoiceDate,
            transaction.ExpirationDate ?? transaction.Policy.ExpirationDate,
            insured.DisplayName,
            insured.State,
            transaction.Policy.ProgramId,
            transaction.Policy.Program?.Name,
            transaction.Policy.CarrierId,
            transaction.Policy.Carrier.Name,
            transaction.Policy.LineOfBusiness,
            invoice.GrossPremium,
            grossCommission,
            invoice.TotalFees,
            invoice.TotalAmount,
            invoice.GrossPremium - grossCommission,
            invoice.InvoiceNumber);
    }

    private static DateOnly ResolveReportingDate(PolicyTransaction transaction, Invoice invoice, BordereauxDateBasis dateBasis)
        => dateBasis switch
        {
            BordereauxDateBasis.EffectiveDate => transaction.EffectiveDate,
            BordereauxDateBasis.BoundDate => invoice.InvoiceDate,
            BordereauxDateBasis.EffectiveOrBoundDateGreater => invoice.InvoiceDate > transaction.EffectiveDate
                ? invoice.InvoiceDate
                : transaction.EffectiveDate,
            _ => invoice.InvoiceDate > transaction.EffectiveDate ? invoice.InvoiceDate : transaction.EffectiveDate,
        };

    private static HashSet<TransactionType> ParseIncludedTransactionTypes(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        var result = new HashSet<TransactionType>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
                continue;
            if (Enum.TryParse<TransactionType>(element.GetString(), ignoreCase: true, out var value))
                result.Add(value);
        }

        return result;
    }

    private static (string Code, string Message)? ValidateJsonArray(string json, string label)
    {
        using var document = TryParse(json, label, out var error);
        if (error is not null)
            return error.Value;
        if (document!.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            return ($"{label}_REQUIRED", $"{LabelToText(label)} must be a non-empty JSON array.");
        return null;
    }

    private static (string Code, string Message)? ValidateJsonObject(string json, string label)
    {
        using var document = TryParse(json, label, out var error);
        if (error is not null)
            return error.Value;
        return document!.RootElement.ValueKind == JsonValueKind.Object
            ? null
            : ($"INVALID_{label}_JSON", $"{LabelToText(label)} must be a JSON object.");
    }

    private static JsonDocument? TryParse(string json, string label, out (string Code, string Message)? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = ($"{label}_REQUIRED", $"{LabelToText(label)} is required.");
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            error = ($"INVALID_{label}_JSON", $"{LabelToText(label)} must be valid JSON.");
            return null;
        }
    }

    private static string NormalizeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement);
    }

    private static string? NormalizeState(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return null;
        var trimmed = stateCode.Trim().ToUpperInvariant();
        return trimmed.Length == 2 ? trimmed : null;
    }

    private static string LabelToText(string label)
        => label.ToLowerInvariant().Replace('_', ' ');

    private sealed record PreviewSourceRow(Invoice Invoice, PolicyTransaction Transaction);
}
