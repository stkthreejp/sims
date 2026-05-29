using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SIMS.Application.Common;
using SIMS.Application.DTOs.SurplusLines;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class SurplusLinesSetupAdminService : ISurplusLinesSetupAdminService
{
    private readonly DbContext _db;

    public SurplusLinesSetupAdminService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<SurplusLinesStateSetupDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = BaseQuery();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        var setups = await query
            .OrderBy(s => s.StateCode)
            .ThenBy(s => s.ProgramConfiguration == null ? string.Empty : s.ProgramConfiguration.Name)
            .ThenBy(s => s.Carrier == null ? string.Empty : s.Carrier.Name)
            .ThenBy(s => s.LineOfBusiness)
            .ThenByDescending(s => s.EffectiveDate)
            .ToListAsync(ct);

        var result = new List<SurplusLinesStateSetupDto>();
        foreach (var setup in setups)
            result.Add(await MapAsync(setup, ct));
        return result;
    }

    public async Task<Result<SurplusLinesStateSetupDto>> GetAsync(Guid id, CancellationToken ct = default)
    {
        var setup = await BaseQuery().SingleOrDefaultAsync(s => s.Id == id, ct);
        return setup is null
            ? Result<SurplusLinesStateSetupDto>.Failure("SURPLUS_LINES_SETUP_NOT_FOUND", "Surplus lines setup was not found.")
            : Result<SurplusLinesStateSetupDto>.Success(await MapAsync(setup, ct));
    }

    public async Task<Result<SurplusLinesStateSetupDto>> CreateAsync(UpsertSurplusLinesStateSetupRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(request, ct);
        if (validation is not null)
            return Result<SurplusLinesStateSetupDto>.Failure(validation.Value.Code, validation.Value.Message);

        var setup = new SurplusLinesStateSetup();
        Apply(setup, request);

        _db.Set<SurplusLinesStateSetup>().Add(setup);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(setup.Id, ct);
    }

    public async Task<Result<SurplusLinesStateSetupDto>> UpdateAsync(Guid id, UpsertSurplusLinesStateSetupRequest request, CancellationToken ct = default)
    {
        var setup = await _db.Set<SurplusLinesStateSetup>().SingleOrDefaultAsync(s => s.Id == id, ct);
        if (setup is null)
            return Result<SurplusLinesStateSetupDto>.Failure("SURPLUS_LINES_SETUP_NOT_FOUND", "Surplus lines setup was not found.");

        var validation = await ValidateAsync(request, ct);
        if (validation is not null)
            return Result<SurplusLinesStateSetupDto>.Failure(validation.Value.Code, validation.Value.Message);

        Apply(setup, request);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(setup.Id, ct);
    }

    public async Task<Result<SurplusLinesStateSetupDto>> CopyAsync(Guid sourceSetupId, CopySurplusLinesStateSetupRequest request, CancellationToken ct = default)
    {
        var targetState = NormalizeStateCode(request.TargetStateCode);
        if (!targetState.IsSuccess)
            return Result<SurplusLinesStateSetupDto>.Failure(targetState.ErrorCode!, targetState.ErrorMessage!);

        var source = await _db.Set<SurplusLinesStateSetup>().SingleOrDefaultAsync(s => s.Id == sourceSetupId, ct);
        if (source is null)
            return Result<SurplusLinesStateSetupDto>.Failure("SURPLUS_LINES_SETUP_NOT_FOUND", "Source surplus lines setup was not found.");

        if (source.StateCode == targetState.Value)
            return Result<SurplusLinesStateSetupDto>.Failure("SURPLUS_LINES_COPY_SAME_STATE", "Target state must be different from source state.");

        var copy = new SurplusLinesStateSetup
        {
            StateCode = targetState.Value!,
            ProgramConfigurationId = source.ProgramConfigurationId,
            CarrierId = source.CarrierId,
            LineOfBusiness = source.LineOfBusiness,
            EffectiveDate = source.EffectiveDate,
            ExpirationDate = source.ExpirationDate,
            IsActive = source.IsActive,
            FilingRequired = source.FilingRequired,
            LicenseHolderType = source.LicenseHolderType,
            FilingBrokerName = source.FilingBrokerName,
            LicenseNumber = source.LicenseNumber,
            LicenseState = source.LicenseState,
            BrokerAddressLine1 = source.BrokerAddressLine1,
            BrokerAddressLine2 = source.BrokerAddressLine2,
            BrokerCity = source.BrokerCity,
            BrokerState = source.BrokerState,
            BrokerZipCode = source.BrokerZipCode,
            BrokerCountry = source.BrokerCountry,
            StampingWording = source.StampingWording,
            RequiredNoticeText = source.RequiredNoticeText,
            PaperworkNotes = source.PaperworkNotes,
            FilingNotes = source.FilingNotes,
            SurplusLinesTaxFeeDefinitionId = source.SurplusLinesTaxFeeDefinitionId,
            StampingFeeDefinitionId = source.StampingFeeDefinitionId,
            FilingFeeDefinitionId = source.FilingFeeDefinitionId,
            FilingPayeeId = source.FilingPayeeId,
            CreateFilingPayable = source.CreateFilingPayable,
            FilingPaymentTermsDays = source.FilingPaymentTermsDays,
            FilingFrequency = source.FilingFrequency,
            FilingDueDayOfMonth = source.FilingDueDayOfMonth,
            FilingMethod = source.FilingMethod,
            FilingPortalUrl = source.FilingPortalUrl,
            RequiredFilingFormsJson = source.RequiredFilingFormsJson,
            DiligentSearchRequired = source.DiligentSearchRequired,
            DiligentSearchNotes = source.DiligentSearchNotes,
            AffidavitRequired = source.AffidavitRequired,
            AffidavitNotes = source.AffidavitNotes
        };

        _db.Set<SurplusLinesStateSetup>().Add(copy);
        await _db.SaveChangesAsync(ct);

        return await GetAsync(copy.Id, ct);
    }

    private IQueryable<SurplusLinesStateSetup> BaseQuery() =>
        _db.Set<SurplusLinesStateSetup>()
            .Include(s => s.ProgramConfiguration)
            .Include(s => s.Carrier)
            .Include(s => s.SurplusLinesTaxFeeDefinition)
            .Include(s => s.StampingFeeDefinition)
            .Include(s => s.FilingFeeDefinition)
            .Include(s => s.FilingPayee);

    private async Task<(string Code, string Message)?> ValidateAsync(UpsertSurplusLinesStateSetupRequest request, CancellationToken ct)
    {
        if (!NormalizeStateCode(request.StateCode).IsSuccess)
            return ("STATE_CODE_INVALID", "State code must be two characters.");
        if (!NormalizeStateCode(request.LicenseState).IsSuccess)
            return ("LICENSE_STATE_INVALID", "License state must be two characters.");
        if (!NormalizeStateCode(request.BrokerState).IsSuccess)
            return ("BROKER_STATE_INVALID", "Broker state must be two characters.");
        if (request.ExpirationDate.HasValue && request.ExpirationDate.Value < request.EffectiveDate)
            return ("INVALID_DATE_RANGE", "Expiration date cannot be before effective date.");
        if (request.CreateFilingPayable && !request.FilingPayeeId.HasValue)
            return ("FILING_PAYEE_REQUIRED", "Select a filing payee before creating filing payables.");
        if (request.FilingPaymentTermsDays is < 0 or > 365)
            return ("INVALID_PAYMENT_TERMS", "Filing payment terms must be between 0 and 365 days.");
        if (request.FilingDueDayOfMonth is < 1 or > 31)
            return ("INVALID_DUE_DAY", "Filing due day must be between 1 and 31.");
        if (!IsValidJsonArray(request.RequiredFilingFormsJson))
            return ("INVALID_REQUIRED_FORMS", "Required filing forms must be a JSON array.");
        if (string.IsNullOrWhiteSpace(request.LicenseHolderType))
            return ("LICENSE_HOLDER_REQUIRED", "License holder type is required.");
        if (string.IsNullOrWhiteSpace(request.FilingBrokerName))
            return ("FILING_BROKER_REQUIRED", "Filing broker name is required.");
        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            return ("LICENSE_NUMBER_REQUIRED", "License number is required.");

        if (request.ProgramConfigurationId.HasValue)
        {
            var programExists = await _db.Set<ProgramConfiguration>()
                .AnyAsync(p => p.Id == request.ProgramConfigurationId.Value, ct);
            if (!programExists)
                return ("PROGRAM_NOT_FOUND", "Program was not found.");
        }

        if (request.CarrierId.HasValue)
        {
            var carrierExists = await _db.Set<Carrier>()
                .AnyAsync(c => c.Id == request.CarrierId.Value, ct);
            if (!carrierExists)
                return ("CARRIER_NOT_FOUND", "Carrier was not found.");
        }

        var feeIds = new[]
            {
                request.SurplusLinesTaxFeeDefinitionId,
                request.StampingFeeDefinitionId,
                request.FilingFeeDefinitionId
            }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (feeIds.Count > 0)
        {
            var foundFeeCount = await _db.Set<FeeDefinition>()
                .CountAsync(f => feeIds.Contains(f.Id), ct);
            if (foundFeeCount != feeIds.Count)
                return ("FEE_DEFINITION_NOT_FOUND", "One or more linked fee definitions were not found.");
        }

        if (request.FilingPayeeId.HasValue)
        {
            var payeeExists = await _db.Set<Payee>()
                .AnyAsync(p => p.Id == request.FilingPayeeId.Value && p.TenantId == 1 && p.IsActive, ct);
            if (!payeeExists)
                return ("FILING_PAYEE_NOT_FOUND", "Filing payee was not found or inactive.");
        }

        return null;
    }

    private static void Apply(SurplusLinesStateSetup setup, UpsertSurplusLinesStateSetupRequest request)
    {
        setup.StateCode = NormalizeStateCode(request.StateCode).Value!;
        setup.ProgramConfigurationId = request.ProgramConfigurationId;
        setup.CarrierId = request.CarrierId;
        setup.LineOfBusiness = request.LineOfBusiness;
        setup.EffectiveDate = request.EffectiveDate;
        setup.ExpirationDate = request.ExpirationDate;
        setup.IsActive = request.IsActive;
        setup.FilingRequired = request.FilingRequired;
        setup.LicenseHolderType = TrimToEmpty(request.LicenseHolderType);
        setup.FilingBrokerName = TrimToEmpty(request.FilingBrokerName);
        setup.LicenseNumber = TrimToEmpty(request.LicenseNumber);
        setup.LicenseState = NormalizeStateCode(request.LicenseState).Value!;
        setup.BrokerAddressLine1 = TrimToEmpty(request.BrokerAddressLine1);
        setup.BrokerAddressLine2 = TrimToNull(request.BrokerAddressLine2);
        setup.BrokerCity = TrimToEmpty(request.BrokerCity);
        setup.BrokerState = NormalizeStateCode(request.BrokerState).Value!;
        setup.BrokerZipCode = TrimToEmpty(request.BrokerZipCode);
        setup.BrokerCountry = string.IsNullOrWhiteSpace(request.BrokerCountry) ? "USA" : request.BrokerCountry.Trim().ToUpperInvariant();
        setup.StampingWording = TrimToNull(request.StampingWording);
        setup.RequiredNoticeText = TrimToNull(request.RequiredNoticeText);
        setup.PaperworkNotes = TrimToNull(request.PaperworkNotes);
        setup.FilingNotes = TrimToNull(request.FilingNotes);
        setup.SurplusLinesTaxFeeDefinitionId = request.SurplusLinesTaxFeeDefinitionId;
        setup.StampingFeeDefinitionId = request.StampingFeeDefinitionId;
        setup.FilingFeeDefinitionId = request.FilingFeeDefinitionId;
        setup.FilingPayeeId = request.FilingPayeeId;
        setup.CreateFilingPayable = request.CreateFilingPayable;
        setup.FilingPaymentTermsDays = request.FilingPaymentTermsDays;
        setup.FilingFrequency = TrimToNull(request.FilingFrequency);
        setup.FilingDueDayOfMonth = request.FilingDueDayOfMonth;
        setup.FilingMethod = TrimToNull(request.FilingMethod);
        setup.FilingPortalUrl = TrimToNull(request.FilingPortalUrl);
        setup.RequiredFilingFormsJson = NormalizeJsonArray(request.RequiredFilingFormsJson);
        setup.DiligentSearchRequired = request.DiligentSearchRequired;
        setup.DiligentSearchNotes = TrimToNull(request.DiligentSearchNotes);
        setup.AffidavitRequired = request.AffidavitRequired;
        setup.AffidavitNotes = TrimToNull(request.AffidavitNotes);
    }

    private static Result<string> NormalizeStateCode(string stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return Result<string>.Failure("STATE_CODE_REQUIRED", "State code is required.");

        var normalized = stateCode.Trim().ToUpperInvariant();
        return normalized.Length == 2
            ? Result<string>.Success(normalized)
            : Result<string>.Failure("STATE_CODE_INVALID", "State code must be two characters.");
    }

    private async Task<SurplusLinesStateSetupDto> MapAsync(SurplusLinesStateSetup setup, CancellationToken ct) =>
        new(
            setup.Id,
            setup.StateCode,
            setup.ProgramConfigurationId,
            setup.ProgramConfiguration?.Name,
            setup.CarrierId,
            setup.Carrier?.Name,
            setup.LineOfBusiness,
            setup.LineOfBusiness.HasValue ? GetLobLabel(setup.LineOfBusiness.Value) : null,
            setup.EffectiveDate,
            setup.ExpirationDate,
            setup.IsActive,
            setup.FilingRequired,
            setup.LicenseHolderType,
            setup.FilingBrokerName,
            setup.LicenseNumber,
            setup.LicenseState,
            setup.BrokerAddressLine1,
            setup.BrokerAddressLine2,
            setup.BrokerCity,
            setup.BrokerState,
            setup.BrokerZipCode,
            setup.BrokerCountry,
            setup.StampingWording,
            setup.RequiredNoticeText,
            setup.PaperworkNotes,
            setup.FilingNotes,
            setup.SurplusLinesTaxFeeDefinitionId,
            setup.SurplusLinesTaxFeeDefinition?.DisplayName,
            setup.StampingFeeDefinitionId,
            setup.StampingFeeDefinition?.DisplayName,
            setup.FilingFeeDefinitionId,
            setup.FilingFeeDefinition?.DisplayName,
            setup.FilingPayeeId,
            setup.FilingPayee?.Name,
            setup.CreateFilingPayable,
            setup.FilingPaymentTermsDays,
            setup.FilingFrequency,
            setup.FilingDueDayOfMonth,
            setup.FilingMethod,
            setup.FilingPortalUrl,
            setup.RequiredFilingFormsJson,
            setup.DiligentSearchRequired,
            setup.DiligentSearchNotes,
            setup.AffidavitRequired,
            setup.AffidavitNotes,
            await GetFeeValidationMessagesAsync(setup, ct),
            setup.CreatedAt,
            setup.UpdatedAt);

    private async Task<IReadOnlyList<string>> GetFeeValidationMessagesAsync(SurplusLinesStateSetup setup, CancellationToken ct)
    {
        var linkedFees = new (long? Id, string Label, string? DisplayName)[]
        {
            (setup.SurplusLinesTaxFeeDefinitionId, "surplus lines tax", setup.SurplusLinesTaxFeeDefinition?.DisplayName),
            (setup.StampingFeeDefinitionId, "stamping fee", setup.StampingFeeDefinition?.DisplayName),
            (setup.FilingFeeDefinitionId, "filing fee", setup.FilingFeeDefinition?.DisplayName),
        };

        var messages = new List<string>();
        foreach (var linkedFee in linkedFees.Where(f => f.Id.HasValue))
        {
            var hasMatchingRule = await _db.Set<FeeRuleVersion>().AnyAsync(v =>
                v.FeeDefinitionId == linkedFee.Id!.Value &&
                v.EffectiveDate <= setup.EffectiveDate &&
                (!v.DisabledDate.HasValue || v.DisabledDate.Value > setup.EffectiveDate) &&
                (v.ProgramConfigurationId == null || v.ProgramConfigurationId == setup.ProgramConfigurationId) &&
                (v.CarrierId == null || v.CarrierId == setup.CarrierId) &&
                (v.LineOfBusiness == null || v.LineOfBusiness == (setup.LineOfBusiness == null ? null : setup.LineOfBusiness.Value.ToString())) &&
                (v.StateCode == null || v.StateCode == setup.StateCode), ct);

            if (!hasMatchingRule)
            {
                var feeName = linkedFee.DisplayName ?? linkedFee.Label;
                messages.Add($"{feeName} is linked, but no active fee rule matches this setup scope and effective date.");
            }
        }

        return messages;
    }

    private static string GetLobLabel(PolicyLineOfBusiness lob) => lob switch
    {
        PolicyLineOfBusiness.GeneralLiability => "General Liability",
        PolicyLineOfBusiness.InlandMarine => "Inland Marine",
        PolicyLineOfBusiness.AutoLiability => "Auto Liability",
        PolicyLineOfBusiness.AutoPhysicalDamage => "Auto Physical Damage",
        _ => lob.ToString()
    };

    private static string TrimToEmpty(string? value) => value?.Trim() ?? string.Empty;
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsValidJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "[]";

        using var document = JsonDocument.Parse(value);
        return JsonSerializer.Serialize(document.RootElement);
    }
}
