using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Bordereaux;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Entities.Bordereaux;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class BordereauxService : IBordereauxService
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly DbContext _db;
    private readonly IBlobStorageService? _blobStorage;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public BordereauxService(DbContext db) : this(db, null)
    {
    }

    public BordereauxService(DbContext db, IBlobStorageService? blobStorage)
    {
        _db = db;
        _blobStorage = blobStorage;
    }

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
        var validationSummary = await BuildRunValidationSummaryAsync(profile, preview.Value.Rows, periodStart, periodEnd, ct);
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
            ValidationSummaryJson = validationSummary,
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

    public async Task<IReadOnlyList<BordereauxRunDto>> GetRunsAsync(Guid? profileId = null, CancellationToken ct = default)
    {
        var query = _db.Set<BordereauxRun>()
            .AsNoTracking()
            .Include(r => r.Profile)
            .AsQueryable();

        if (profileId.HasValue)
            query = query.Where(r => r.BordereauxProfileId == profileId.Value);

        var runs = await query
            .OrderByDescending(r => r.PeriodEnd)
            .ThenByDescending(r => r.RunNumber)
            .ToListAsync(ct);

        return runs.Select(r => MapRun(r, r.Profile.Name)).ToList();
    }

    public async Task<Result<BordereauxRunDto>> GetRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.Set<BordereauxRun>()
            .AsNoTracking()
            .Include(r => r.Profile)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        return run is null
            ? Result<BordereauxRunDto>.Failure("RUN_NOT_FOUND", "Bordereaux run not found.")
            : Result<BordereauxRunDto>.Success(MapRun(run, run.Profile.Name));
    }

    public async Task<Result<BordereauxRunDto>> GeneratePremiumExportPackageAsync(
        Guid runId,
        Guid? generatedById,
        CancellationToken ct = default)
    {
        if (_blobStorage is null)
            return Result<BordereauxRunDto>.Failure("BLOB_STORAGE_NOT_CONFIGURED", "Blob storage is not configured.");

        var run = await _db.Set<BordereauxRun>()
            .Include(r => r.Profile).ThenInclude(p => p.ProgramConfiguration)
            .Include(r => r.Profile).ThenInclude(p => p.Carrier)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return Result<BordereauxRunDto>.Failure("RUN_NOT_FOUND", "Bordereaux run not found.");

        var rows = JsonSerializer.Deserialize<List<BordereauxPremiumPreviewRowDto>>(run.SourceRowsSnapshotJson, SnapshotJsonOptions) ?? [];
        var requiredTabs = ParseStringArray(run.Profile.RequiredTabsJson);
        var londonRows = await BuildLondonRowsAsync(run, rows, ct);
        var londonBytes = BordereauxWorkbookBuilder.BuildLondonBordereaux(londonRows, requiredTabs);
        var accountCurrentBytes = BordereauxWorkbookBuilder.BuildAccountCurrent(rows);
        var periodLabel = run.PeriodStart.ToString("yyyy-MM");
        var programName = SafeFilePart(run.Profile.ProgramConfiguration.Name);
        var carrierName = SafeFilePart(run.Profile.Carrier.Name);
        var londonFileName = $"{programName}-{carrierName}-London-BDX-{periodLabel}-run-{run.RunNumber}.xlsx";
        var accountCurrentFileName = $"{programName}-{carrierName}-Account-Current-{periodLabel}-run-{run.RunNumber}.xlsx";

        await using var londonStream = new MemoryStream(londonBytes);
        await using var accountCurrentStream = new MemoryStream(accountCurrentBytes);
        run.LondonBordereauxBlobPath = await _blobStorage.UploadAsync(londonStream, londonFileName, XlsxContentType);
        run.LondonBordereauxFileName = londonFileName;
        run.LondonBordereauxContentType = XlsxContentType;
        run.AccountCurrentBlobPath = await _blobStorage.UploadAsync(accountCurrentStream, accountCurrentFileName, XlsxContentType);
        run.AccountCurrentFileName = accountCurrentFileName;
        run.AccountCurrentContentType = XlsxContentType;
        run.Status = BordereauxRunStatus.Generated;
        run.GeneratedById = generatedById;
        run.GeneratedAt = DateTime.UtcNow;
        run.BordereauxRowCount = rows.Count;
        run.AccountCurrentRowCount = rows.Count;
        run.DetailRowCountsJson = JsonSerializer.Serialize(new
        {
            premiumRows = rows.Count,
            autoVehicleRows = londonRows.Sum(row => row.AutoVehicles.Count),
            imUnitRows = londonRows.Sum(row => row.ImUnits.Count),
            londonBordereauxSha256 = Sha256(londonBytes),
            accountCurrentSha256 = Sha256(accountCurrentBytes),
        }, SnapshotJsonOptions);
        run.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<BordereauxRunDto>.Success(MapRun(run, run.Profile.Name));
    }

    public async Task<Result<string>> GetRunFileDownloadUrlAsync(
        Guid runId,
        BordereauxRunFileKind fileKind,
        CancellationToken ct = default)
    {
        if (_blobStorage is null)
            return Result<string>.Failure("BLOB_STORAGE_NOT_CONFIGURED", "Blob storage is not configured.");

        var run = await _db.Set<BordereauxRun>()
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return Result<string>.Failure("RUN_NOT_FOUND", "Bordereaux run not found.");

        var (blobPath, fileName) = fileKind switch
        {
            BordereauxRunFileKind.LondonBordereaux => (run.LondonBordereauxBlobPath, run.LondonBordereauxFileName),
            BordereauxRunFileKind.AccountCurrent => (run.AccountCurrentBlobPath, run.AccountCurrentFileName),
            _ => (null, null),
        };

        if (string.IsNullOrWhiteSpace(blobPath) || string.IsNullOrWhiteSpace(fileName))
            return Result<string>.Failure("FILE_NOT_GENERATED", "The requested bordereaux file has not been generated yet.");

        var url = await _blobStorage.GetDownloadUrlAsync(blobPath, fileName);
        return Result<string>.Success(url);
    }

    public async Task<Result<BordereauxRunDto>> ReconcilePremiumRunAsync(
        Guid runId,
        ReconcileBordereauxRunRequest request,
        CancellationToken ct = default)
    {
        var run = await _db.Set<BordereauxRun>()
            .Include(r => r.Profile)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
            return Result<BordereauxRunDto>.Failure("RUN_NOT_FOUND", "Bordereaux run not found.");

        var rows = JsonSerializer.Deserialize<List<BordereauxPremiumPreviewRowDto>>(run.SourceRowsSnapshotJson, SnapshotJsonOptions) ?? [];
        var grossPremiumTotal = rows.Sum(r => r.GrossPremium);
        var grossCommissionTotal = rows.Sum(r => r.GrossCommission);
        var feesTotal = rows.Sum(r => r.Fees);
        var netDueCarrierTotal = rows.Sum(r => r.NetDueCarrier);

        var grossPremiumDifference = grossPremiumTotal - request.AccountCurrentGrossPremiumTotal;
        var grossCommissionDifference = grossCommissionTotal - request.AccountCurrentGrossCommissionTotal;
        var feesDifference = feesTotal - request.AccountCurrentFeesTotal;
        var netDueCarrierDifference = netDueCarrierTotal - request.AccountCurrentNetDueCarrierTotal;
        var rowCountDifference = rows.Count - request.AccountCurrentRowCount;
        var matched = grossPremiumDifference == 0m
            && grossCommissionDifference == 0m
            && feesDifference == 0m
            && netDueCarrierDifference == 0m
            && rowCountDifference == 0;

        run.AccountCurrentRowCount = request.AccountCurrentRowCount;
        run.ReconciliationStatus = matched
            ? BordereauxReconciliationStatus.Matched
            : BordereauxReconciliationStatus.Mismatch;
        run.ReconciliationSummaryJson = JsonSerializer.Serialize(new
        {
            status = matched ? "matched" : "mismatch",
            bordereaux = new
            {
                rowCount = rows.Count,
                grossPremiumTotal,
                grossCommissionTotal,
                feesTotal,
                netDueCarrierTotal,
            },
            accountCurrent = new
            {
                rowCount = request.AccountCurrentRowCount,
                grossPremiumTotal = request.AccountCurrentGrossPremiumTotal,
                grossCommissionTotal = request.AccountCurrentGrossCommissionTotal,
                feesTotal = request.AccountCurrentFeesTotal,
                netDueCarrierTotal = request.AccountCurrentNetDueCarrierTotal,
            },
            differences = new
            {
                rowCountDifference,
                grossPremiumDifference,
                grossCommissionDifference,
                feesDifference,
                netDueCarrierDifference,
            },
        }, SnapshotJsonOptions);
        run.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Result<BordereauxRunDto>.Success(MapRun(run, run.Profile.Name));
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
        run.LondonBordereauxBlobPath,
        run.LondonBordereauxFileName,
        run.LondonBordereauxContentType,
        run.AccountCurrentBlobPath,
        run.AccountCurrentFileName,
        run.AccountCurrentContentType,
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
        var policy = transaction.Policy;
        var grossCommission = invoice.CommissionAmount;
        var insuredAddress = FormatAddress(insured.AddressLine1, insured.AddressLine2);
        var newRenewal = transaction.TransactionType == TransactionType.Renewal || policy.PolicyTermNumber > 1
            ? "Renewal"
            : "New";

        return new BordereauxPremiumPreviewRowDto(
            transaction.PolicyId,
            transaction.Id,
            invoice.Id,
            policy.PolicyNumber,
            transaction.TransactionNumber,
            transaction.TransactionType,
            reportingDate,
            transaction.EffectiveDate,
            invoice.InvoiceDate,
            transaction.ExpirationDate ?? policy.ExpirationDate,
            insured.DisplayName,
            insured.State,
            policy.ProgramId,
            policy.Program?.Name,
            policy.CarrierId,
            policy.Carrier.Name,
            policy.LineOfBusiness,
            invoice.GrossPremium,
            grossCommission,
            invoice.TotalFees,
            invoice.TotalAmount,
            invoice.GrossPremium - grossCommission,
            invoice.InvoiceNumber,
            insuredAddress,
            insured.ZipCode,
            insured.County ?? string.Empty,
            policy.IssuedDate ?? policy.BoundDate,
            insured.OperationType ?? string.Empty,
            newRenewal);
    }

    private async Task<IReadOnlyList<BordereauxLondonPremiumRow>> BuildLondonRowsAsync(
        BordereauxRun run,
        IReadOnlyList<BordereauxPremiumPreviewRowDto> rows,
        CancellationToken ct)
    {
        var staticValues = ParseJsonObject(run.Profile.StaticValuesJson);
        var coverholderName = GetStaticValue(staticValues, "coverholderName") ?? "Specialty Market Managers, LLC";
        var coverholderPin = GetStaticValue(staticValues, "coverholderPin") ?? "USA00060";
        var profileUmr = GetStaticValue(staticValues, "umr") ?? string.Empty;
        var yearOfAccount = GetStaticValue(staticValues, "yearOfAccount") ?? string.Empty;
        var currencyCode = string.IsNullOrWhiteSpace(run.Profile.Carrier.DefaultCurrencyCode)
            ? "USD"
            : run.Profile.Carrier.DefaultCurrencyCode.Trim().ToUpperInvariant();

        var lobSetups = await _db.Set<ProgramCarrierLineOfBusiness>()
            .Include(l => l.ProgramCarrier)
            .Where(l => l.ProgramCarrier.ProgramConfigurationId == run.Profile.ProgramConfigurationId
                && l.ProgramCarrier.CarrierId == run.Profile.CarrierId
                && l.ProgramCarrier.IsActive
                && l.IsActive)
            .ToListAsync(ct);
        var commissionRows = await _db.Set<CarrierCommission>()
            .Where(c => c.CarrierId == run.Profile.CarrierId
                && (c.ProgramConfigurationId == run.Profile.ProgramConfigurationId || c.ProgramConfigurationId == null)
                && c.EffectiveDate <= run.PeriodEnd
                && (c.DisabledDate == null || c.DisabledDate > run.PeriodStart))
            .ToListAsync(ct);
        var surplusLinesSetups = await _db.Set<SurplusLinesStateSetup>()
            .Where(s => s.IsActive
                && s.EffectiveDate <= run.PeriodEnd
                && (s.ExpirationDate == null || s.ExpirationDate >= run.PeriodStart)
                && (s.ProgramConfigurationId == run.Profile.ProgramConfigurationId || s.ProgramConfigurationId == null)
                && (s.CarrierId == run.Profile.CarrierId || s.CarrierId == null))
            .ToListAsync(ct);
        var intermediarySetups = await _db.Set<IntermediaryProgramCarrierLobSetup>()
            .Include(s => s.Intermediary)
            .Where(s => s.IsActive
                && s.Intermediary.IsActive
                && s.EffectiveDate <= run.PeriodEnd
                && (s.ExpirationDate == null || s.ExpirationDate >= run.PeriodStart)
                && s.ProgramConfigurationId == run.Profile.ProgramConfigurationId
                && s.CarrierId == run.Profile.CarrierId)
            .ToListAsync(ct);
        var detailRows = await BuildLondonDetailRowsAsync(rows, ct);

        return rows.Select(row =>
        {
            var setup = ResolveLobSetup(lobSetups, row.LineOfBusiness, row.ReportingDate);
            var surplusLinesSetup = ResolveSurplusLinesSetup(surplusLinesSetups, row, row.ReportingDate, run.Profile.ProgramConfigurationId);
            var intermediarySetup = ResolveIntermediarySetup(intermediarySetups, row.LineOfBusiness, row.ReportingDate);
            var commissionRate = ResolveCarrierCommissionRate(commissionRows, row.LineOfBusiness, row.ReportingDate, run.Profile.ProgramConfigurationId)
                ?? (row.GrossPremium == 0 ? 0 : decimal.Round(row.GrossCommission / row.GrossPremium, 6));
            var commissionAmount = decimal.Round(row.GrossPremium * commissionRate, 2, MidpointRounding.AwayFromZero);
            var brokerageRate = intermediarySetup?.BrokerageRate;
            var brokerageAmount = brokerageRate.HasValue
                ? decimal.Round(row.GrossPremium * brokerageRate.Value, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;
            var umr = setup?.LondonUmr ?? profileUmr;
            detailRows.TryGetValue(row.PolicyTransactionId, out var details);

            return new BordereauxLondonPremiumRow(
                row,
                run.PeriodStart,
                run.PeriodEnd,
                coverholderName,
                coverholderPin,
                umr,
                setup?.LondonSectionNumber ?? string.Empty,
                setup?.LondonClassOfBusiness ?? string.Empty,
                setup?.LondonRiskCode ?? string.Empty,
                setup?.LondonInsuranceType ?? "DIRECT",
                yearOfAccount,
                currencyCode,
                commissionRate,
                commissionAmount,
                row.GrossPremium - commissionAmount - (brokerageAmount ?? 0m),
                surplusLinesSetup?.StateCode ?? row.InsuredState,
                surplusLinesSetup?.FilingBrokerName ?? string.Empty,
                surplusLinesSetup?.LicenseNumber ?? string.Empty,
                null,
                surplusLinesSetup == null ? string.Empty : FormatAddress(surplusLinesSetup.BrokerAddressLine1, surplusLinesSetup.BrokerAddressLine2),
                surplusLinesSetup?.BrokerState ?? string.Empty,
                surplusLinesSetup?.BrokerZipCode ?? string.Empty,
                surplusLinesSetup?.BrokerCountry ?? string.Empty,
                brokerageRate,
                brokerageAmount,
                details?.PrimaryRiskLocationAddress ?? row.InsuredAddress,
                details?.PrimaryRiskLocationCounty ?? row.InsuredCounty,
                details?.PrimaryRiskLocationPostcode ?? row.InsuredPostcode,
                details?.SumInsuredAmount,
                details?.AggregateSumInsuredAmount,
                details?.TotalInsurableValue,
                details?.DeductibleAmount,
                details?.DeductibleBasis ?? string.Empty,
                row.Fees == 0m ? string.Empty : "State Taxes and Fees",
                row.Fees == 0m ? null : row.Fees,
                details?.Agent is null ? string.Empty : "Producing Agents and Brokers",
                AgentName(details?.Agent),
                details?.Agent?.LicenseNumber ?? string.Empty,
                FormatAgentAddress(details?.AgentLocation),
                details?.AgentLocation?.State ?? string.Empty,
                details?.AgentLocation?.ZipCode ?? string.Empty,
                "USA",
                details?.Logging97111Payroll,
                details?.Logging97111Premium,
                details?.LlEndLimit,
                details?.ImRate,
                details?.DebitCreditMod,
                details?.AutoVehicles ?? [],
                details?.ImUnits ?? []);
        }).ToList();
    }

    private async Task<Dictionary<Guid, LondonDetailRows>> BuildLondonDetailRowsAsync(
        IReadOnlyList<BordereauxPremiumPreviewRowDto> rows,
        CancellationToken ct)
    {
        var transactionIds = rows.Select(r => r.PolicyTransactionId).Distinct().ToList();
        if (transactionIds.Count == 0)
            return [];

        var transactions = await _db.Set<PolicyTransaction>()
            .AsNoTracking()
            .Include(t => t.Policy).ThenInclude(p => p.BoundQuote)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Vehicles)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Equipment).ThenInclude(e => e.EquipmentType)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Locations)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.GLCoverages)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.IMCoverages)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.GLClassifications)
            .Include(t => t.Policy).ThenInclude(p => p.Submission).ThenInclude(s => s.Agent).ThenInclude(a => a!.Locations)
            .Where(t => transactionIds.Contains(t.Id))
            .ToListAsync(ct);
        var ratingSnapshots = await _db.Set<QuoteRatingSnapshot>()
            .AsNoTracking()
            .Include(s => s.Lines)
            .Where(s => s.PolicyTransactionId != null && transactionIds.Contains(s.PolicyTransactionId.Value))
            .OrderByDescending(s => s.RatedAt)
            .ToListAsync(ct);
        var ratingByTransactionId = ratingSnapshots
            .GroupBy(s => s.PolicyTransactionId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return transactions.ToDictionary(
            t => t.Id,
            t =>
            {
                ratingByTransactionId.TryGetValue(t.Id, out var ratingSnapshot);
                var primaryLocation = ResolvePrimaryRiskLocation(t.Policy.Submission);
                var agentLocation = ResolvePrimaryAgentLocation(t.Policy.Submission.Agent);
                var ratingValues = ResolveLondonRatingValues(t, ratingSnapshot);

                return new LondonDetailRows(
                    PrimaryRiskLocationAddress: primaryLocation?.Address ?? FormatInsuredAddress(t.Policy.Submission.Insured),
                    PrimaryRiskLocationCounty: primaryLocation?.County ?? t.Policy.Submission.Insured.County ?? string.Empty,
                    PrimaryRiskLocationPostcode: primaryLocation?.ZipCode ?? t.Policy.Submission.Insured.ZipCode,
                    Agent: t.Policy.Submission.Agent,
                    AgentLocation: agentLocation,
                    SumInsuredAmount: ratingValues.SumInsuredAmount,
                    AggregateSumInsuredAmount: ratingValues.AggregateSumInsuredAmount,
                    TotalInsurableValue: ratingValues.TotalInsurableValue,
                    DeductibleAmount: ratingValues.DeductibleAmount,
                    DeductibleBasis: ratingValues.DeductibleBasis,
                    Logging97111Payroll: ratingValues.Logging97111Payroll,
                    Logging97111Premium: ratingValues.Logging97111Premium,
                    LlEndLimit: ratingValues.LlEndLimit,
                    ImRate: ratingValues.ImRate,
                    DebitCreditMod: ratingValues.DebitCreditMod,
                    AutoVehicles: t.Policy.LineOfBusiness is PolicyLineOfBusiness.AutoLiability or PolicyLineOfBusiness.AutoPhysicalDamage
                    ? t.Policy.Submission.Vehicles
                        .OrderBy(v => v.UnitNumber)
                        .Select(v => new BordereauxAutoVehicleDetail(
                            t.Policy.PolicyNumber,
                            v.UnitNumber,
                            v.Year,
                            v.Make ?? string.Empty,
                            v.Model ?? string.Empty,
                            v.Vin ?? string.Empty,
                            v.VehicleClass.ToString(),
                            v.ApdStatedValue,
                            v.ApdCompDeductible ?? v.ApdCollDeductible,
                            null,
                            null))
                        .ToList()
                    : [],
                    ImUnits: t.Policy.LineOfBusiness == PolicyLineOfBusiness.InlandMarine
                    ? t.Policy.Submission.Equipment
                        .OrderBy(e => e.ItemNumber)
                        .Select(e => new BordereauxInlandMarineUnitDetail(
                            t.Policy.PolicyNumber,
                            e.ItemNumber,
                            e.Year,
                            e.Make ?? string.Empty,
                            e.Model ?? e.Description ?? string.Empty,
                            e.SerialNumber ?? string.Empty,
                            e.EquipmentType?.Name ?? string.Empty,
                            e.Value,
                            e.SettlementBasis ?? string.Empty,
                            e.Deductible,
                            null,
                            null,
                            LondonTransactionCode(t.TransactionType, t.PremiumChange)))
                        .ToList()
                    : []);
            });
    }

    private static SubmissionLocation? ResolvePrimaryRiskLocation(Submission submission)
        => submission.Locations
            .Where(l => !l.IsDeleted)
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.LocationNumber)
            .FirstOrDefault();

    private static AgentLocation? ResolvePrimaryAgentLocation(Agent? agent)
        => agent?.Locations
            .Where(l => !l.IsDeleted)
            .OrderByDescending(l => l.IsPrimary)
            .ThenBy(l => l.Name)
            .FirstOrDefault();

    private static LondonRatingValues ResolveLondonRatingValues(PolicyTransaction transaction, QuoteRatingSnapshot? snapshot)
    {
        var submission = transaction.Policy.Submission;
        var quote = transaction.Policy.BoundQuote;
        var scheduleModifier = snapshot?.ScheduleModifier;

        if (transaction.Policy.LineOfBusiness == PolicyLineOfBusiness.GeneralLiability)
        {
            var loggingLine = snapshot?.Lines.FirstOrDefault(l => RatingInputString(l.Inputs, "class_code") == "97111");
            return new LondonRatingValues(
                SumInsuredAmount: submission.GLCoverages?.EachOccurrence ?? quote.Limit,
                AggregateSumInsuredAmount: submission.GLCoverages?.GeneralAggregate,
                TotalInsurableValue: null,
                DeductibleAmount: quote.Deductible,
                DeductibleBasis: quote.Deductible.HasValue ? "Deductible" : string.Empty,
                Logging97111Payroll: loggingLine is null
                    ? submission.GLClassifications.FirstOrDefault(c => c.ClassCode == "97111")?.Exposure
                    : RatingInputDecimal(loggingLine.Inputs, "exposure"),
                Logging97111Premium: loggingLine?.LinePremium,
                LlEndLimit: loggingLine is null ? submission.GLCoverages?.EachOccurrence : RatingInputDecimal(loggingLine.Inputs, "occ_limit"),
                ImRate: null,
                DebitCreditMod: scheduleModifier);
        }

        if (transaction.Policy.LineOfBusiness == PolicyLineOfBusiness.InlandMarine)
        {
            var equipmentValues = submission.Equipment
                .Where(e => !e.IsDeleted && e.Value.HasValue)
                .Select(e => e.Value!.Value)
                .ToList();
            var tiv = equipmentValues.Sum();
            var deductible = submission.Equipment
                .Where(e => !e.IsDeleted && e.Deductible.HasValue)
                .Select(e => e.Deductible!.Value)
                .DefaultIfEmpty(submission.IMCoverages?.Deductible ?? 0m)
                .Where(v => v > 0)
                .DefaultIfEmpty()
                .Min();
            decimal? rate = tiv == 0m || snapshot is null
                ? null
                : decimal.Round(snapshot.GrandTotalPremium / tiv * 100m, 6);

            return new LondonRatingValues(
                SumInsuredAmount: equipmentValues.Count == 0 ? submission.IMCoverages?.MaximumValueAnyOneItem : equipmentValues.Max(),
                AggregateSumInsuredAmount: submission.IMCoverages?.ScheduledEquipmentTotalLimit ?? tiv,
                TotalInsurableValue: tiv == 0m ? null : tiv,
                DeductibleAmount: deductible == 0m ? null : deductible,
                DeductibleBasis: deductible == 0m ? string.Empty : "Deductible",
                Logging97111Payroll: null,
                Logging97111Premium: null,
                LlEndLimit: null,
                ImRate: rate,
                DebitCreditMod: scheduleModifier);
        }

        var autoTiv = submission.Vehicles
            .Where(v => !v.IsDeleted && v.ApdStatedValue.HasValue)
            .Sum(v => v.ApdStatedValue!.Value);
        var autoDeductible = submission.Vehicles
            .Where(v => !v.IsDeleted)
            .Select(v => v.ApdCompDeductible ?? v.ApdCollDeductible)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .DefaultIfEmpty()
            .Min();

        return new LondonRatingValues(
            SumInsuredAmount: quote.Limit,
            AggregateSumInsuredAmount: autoTiv == 0m ? null : autoTiv,
            TotalInsurableValue: autoTiv == 0m ? null : autoTiv,
            DeductibleAmount: autoDeductible == 0m ? null : autoDeductible,
            DeductibleBasis: autoDeductible == 0m ? string.Empty : "Deductible",
            Logging97111Payroll: null,
            Logging97111Premium: null,
            LlEndLimit: null,
            ImRate: null,
            DebitCreditMod: scheduleModifier);
    }

    private static string AgentName(Agent? agent)
        => agent is null ? string.Empty : agent.AgencyName ?? agent.Name;

    private static string FormatAgentAddress(AgentLocation? location)
        => location is null ? string.Empty : FormatAddress(location.AddressLine1 ?? string.Empty, location.AddressLine2);

    private static string FormatInsuredAddress(Insured insured)
        => FormatAddress(insured.AddressLine1, insured.AddressLine2);

    private static decimal? RatingInputDecimal(string json, string propertyName)
    {
        using var document = TryParseJsonDocument(json);
        if (document is null)
            return null;

        return document.RootElement.TryGetProperty(propertyName, out var property)
            ? property.ValueKind switch
            {
                JsonValueKind.Number when property.TryGetDecimal(out var value) => value,
                JsonValueKind.String when decimal.TryParse(property.GetString(), out var value) => value,
                _ => null,
            }
            : null;
    }

    private static string? RatingInputString(string json, string propertyName)
    {
        using var document = TryParseJsonDocument(json);
        if (document is null)
            return null;

        return document.RootElement.TryGetProperty(propertyName, out var property)
            ? property.ValueKind switch
            {
                JsonValueKind.String => property.GetString(),
                JsonValueKind.Number => property.GetRawText(),
                _ => null,
            }
            : null;
    }

    private static JsonDocument? TryParseJsonDocument(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string> BuildRunValidationSummaryAsync(
        BordereauxProfile profile,
        IReadOnlyList<BordereauxPremiumPreviewRowDto> rows,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct)
    {
        var lobSetups = await _db.Set<ProgramCarrierLineOfBusiness>()
            .Include(l => l.ProgramCarrier)
            .Where(l => l.ProgramCarrier.ProgramConfigurationId == profile.ProgramConfigurationId
                && l.ProgramCarrier.CarrierId == profile.CarrierId
                && l.ProgramCarrier.IsActive
                && l.IsActive
                && l.EffectiveDate <= periodEnd
                && (l.ExpirationDate == null || l.ExpirationDate >= periodStart)
                && l.ProgramCarrier.EffectiveDate <= periodEnd
                && (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= periodStart))
            .ToListAsync(ct);

        var surplusLinesSetups = await _db.Set<SurplusLinesStateSetup>()
            .Where(s => s.IsActive
                && s.EffectiveDate <= periodEnd
                && (s.ExpirationDate == null || s.ExpirationDate >= periodStart)
                && (s.ProgramConfigurationId == profile.ProgramConfigurationId || s.ProgramConfigurationId == null)
                && (s.CarrierId == profile.CarrierId || s.CarrierId == null))
            .ToListAsync(ct);

        var missingLondonRows = rows
            .Where(row => ResolveLobSetup(lobSetups, row.LineOfBusiness, row.ReportingDate) is null)
            .ToList();
        var missingSurplusLinesRows = rows
            .Where(row => ResolveSurplusLinesSetup(surplusLinesSetups, row, row.ReportingDate, profile.ProgramConfigurationId) is null)
            .ToList();

        var warnings = new List<object>();
        warnings.AddRange(missingLondonRows.Select(row => new
        {
            code = "MISSING_LONDON_LOB_SETUP",
            row.PolicyNumber,
            row.TransactionNumber,
            row.LineOfBusiness,
            row.ReportingDate,
            message = "No active Program > Carrier > LOB London setup matched this row."
        }));
        warnings.AddRange(missingSurplusLinesRows.Select(row => new
        {
            code = "MISSING_SURPLUS_LINES_SETUP",
            row.PolicyNumber,
            row.TransactionNumber,
            row.LineOfBusiness,
            row.InsuredState,
            row.ReportingDate,
            message = "No active surplus lines setup matched this row's program, carrier, LOB, state, and reporting date."
        }));

        return JsonSerializer.Serialize(new
        {
            status = warnings.Count == 0 ? "clear" : "warnings",
            errors = 0,
            warnings = warnings.Count,
            periodStart,
            periodEnd,
            rowCount = rows.Count,
            missingLondonLobSetupRows = missingLondonRows.Count,
            missingSurplusLinesSetupRows = missingSurplusLinesRows.Count,
            items = warnings,
        }, SnapshotJsonOptions);
    }

    private static string LondonTransactionCode(TransactionType transactionType, decimal grossPremium)
        => transactionType switch
        {
            TransactionType.Endorsement => grossPremium < 0 ? "RP" : "AP",
            TransactionType.Cancellation => "CP",
            TransactionType.Reinstatement => "RN",
            _ => "OP",
        };

    private static string Sha256(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

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

    private static IReadOnlyList<string> ParseStringArray(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return document.RootElement
            .EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
            .Select(element => element.GetString()!.Trim())
            .ToList();
    }

    private static Dictionary<string, string> ParseJsonObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return [];

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
                values[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        return values;
    }

    private static string? GetStaticValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string FormatAddress(string addressLine1, string? addressLine2)
        => string.IsNullOrWhiteSpace(addressLine2)
            ? addressLine1.Trim()
            : $"{addressLine1.Trim()} {addressLine2.Trim()}";

    private static ProgramCarrierLineOfBusiness? ResolveLobSetup(
        IReadOnlyList<ProgramCarrierLineOfBusiness> setups,
        PolicyLineOfBusiness lineOfBusiness,
        DateOnly asOfDate)
        => setups
            .Where(l => l.LineOfBusiness == lineOfBusiness
                && l.EffectiveDate <= asOfDate
                && (l.ExpirationDate == null || l.ExpirationDate >= asOfDate)
                && l.ProgramCarrier.EffectiveDate <= asOfDate
                && (l.ProgramCarrier.ExpirationDate == null || l.ProgramCarrier.ExpirationDate >= asOfDate))
            .OrderByDescending(l => l.EffectiveDate)
            .FirstOrDefault();

    private static decimal? ResolveCarrierCommissionRate(
        IReadOnlyList<CarrierCommission> commissions,
        PolicyLineOfBusiness lineOfBusiness,
        DateOnly asOfDate,
        Guid programConfigurationId)
        => commissions
            .Where(c => (c.LineOfBusiness == lineOfBusiness.ToString() || c.LineOfBusiness == null)
                && c.EffectiveDate <= asOfDate
                && (c.DisabledDate == null || c.DisabledDate > asOfDate))
            .OrderByDescending(c => c.ProgramConfigurationId == programConfigurationId ? 1 : 0)
            .ThenByDescending(c => c.LineOfBusiness == lineOfBusiness.ToString() ? 1 : 0)
            .ThenByDescending(c => c.EffectiveDate)
            .Select(c => (decimal?)c.CommissionRate)
            .FirstOrDefault();

    private static IntermediaryProgramCarrierLobSetup? ResolveIntermediarySetup(
        IReadOnlyList<IntermediaryProgramCarrierLobSetup> setups,
        PolicyLineOfBusiness lineOfBusiness,
        DateOnly asOfDate)
        => setups
            .Where(s => (s.LineOfBusiness == lineOfBusiness || s.LineOfBusiness == null)
                && s.EffectiveDate <= asOfDate
                && (s.ExpirationDate == null || s.ExpirationDate >= asOfDate))
            .OrderByDescending(s => s.LineOfBusiness == lineOfBusiness ? 1 : 0)
            .ThenByDescending(s => s.EffectiveDate)
            .FirstOrDefault();

    private static SurplusLinesStateSetup? ResolveSurplusLinesSetup(
        IReadOnlyList<SurplusLinesStateSetup> setups,
        BordereauxPremiumPreviewRowDto row,
        DateOnly asOfDate,
        Guid programConfigurationId)
        => setups
            .Where(s => s.StateCode == row.InsuredState
                && (s.ProgramConfigurationId == programConfigurationId || s.ProgramConfigurationId == null)
                && (s.CarrierId == row.CarrierId || s.CarrierId == null)
                && (s.LineOfBusiness == row.LineOfBusiness || s.LineOfBusiness == null)
                && s.EffectiveDate <= asOfDate
                && (s.ExpirationDate == null || s.ExpirationDate >= asOfDate))
            .OrderByDescending(s => s.ProgramConfigurationId == programConfigurationId ? 1 : 0)
            .ThenByDescending(s => s.CarrierId == row.CarrierId ? 1 : 0)
            .ThenByDescending(s => s.LineOfBusiness == row.LineOfBusiness ? 1 : 0)
            .ThenByDescending(s => s.EffectiveDate)
            .FirstOrDefault();

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

    private static string SafeFilePart(string value)
    {
        var cleaned = new string(value
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray())
            .Trim('-');
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(cleaned) ? "bordereaux" : cleaned;
    }

    private sealed record PreviewSourceRow(Invoice Invoice, PolicyTransaction Transaction);
    private sealed record LondonDetailRows(
        string PrimaryRiskLocationAddress,
        string PrimaryRiskLocationCounty,
        string PrimaryRiskLocationPostcode,
        Agent? Agent,
        AgentLocation? AgentLocation,
        decimal? SumInsuredAmount,
        decimal? AggregateSumInsuredAmount,
        decimal? TotalInsurableValue,
        decimal? DeductibleAmount,
        string DeductibleBasis,
        decimal? Logging97111Payroll,
        decimal? Logging97111Premium,
        decimal? LlEndLimit,
        decimal? ImRate,
        decimal? DebitCreditMod,
        IReadOnlyList<BordereauxAutoVehicleDetail> AutoVehicles,
        IReadOnlyList<BordereauxInlandMarineUnitDetail> ImUnits);

    private sealed record LondonRatingValues(
        decimal? SumInsuredAmount,
        decimal? AggregateSumInsuredAmount,
        decimal? TotalInsurableValue,
        decimal? DeductibleAmount,
        string DeductibleBasis,
        decimal? Logging97111Payroll,
        decimal? Logging97111Premium,
        decimal? LlEndLimit,
        decimal? ImRate,
        decimal? DebitCreditMod);
}
