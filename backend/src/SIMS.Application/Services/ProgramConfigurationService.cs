using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class ProgramConfigurationService : IProgramConfigurationService
{
    private readonly DbContext _db;

    public ProgramConfigurationService(DbContext db) => _db = db;

    public async Task<IReadOnlyList<ProgramConfigurationDto>> GetAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.Set<ProgramConfiguration>()
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.Carrier)
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.LinesOfBusiness)
                    .ThenInclude(l => l.States)
            .AsQueryable();

        if (!includeInactive)
            query = query.Where(p => p.IsActive);

        var programs = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        return programs.Select(Map).ToList();
    }

    public async Task<Result<ProgramConfigurationDto>> CreateAsync(CreateProgramConfigurationRequest request, CancellationToken ct = default)
    {
        var validation = await ValidateAsync(null, request.Name, request.Code, ct);
        if (validation is not null)
            return Result<ProgramConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        var program = new ProgramConfiguration
        {
            Name = request.Name.Trim(),
            Code = NormalizeCode(request.Code),
            IsActive = request.IsActive,
            Notes = TrimToNull(request.Notes)
        };

        _db.Set<ProgramConfiguration>().Add(program);
        await _db.SaveChangesAsync(ct);

        return Result<ProgramConfigurationDto>.Success(Map(program));
    }

    public async Task<Result<ProgramConfigurationDto>> UpdateAsync(Guid id, UpdateProgramConfigurationRequest request, CancellationToken ct = default)
    {
        var program = await _db.Set<ProgramConfiguration>()
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.Carrier)
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.LinesOfBusiness)
                    .ThenInclude(l => l.States)
            .SingleOrDefaultAsync(p => p.Id == id, ct);

        if (program is null)
            return Result<ProgramConfigurationDto>.Failure("PROGRAM_NOT_FOUND", "Program was not found.");

        var validation = await ValidateAsync(id, request.Name, request.Code, ct);
        if (validation is not null)
            return Result<ProgramConfigurationDto>.Failure(validation.Value.Code, validation.Value.Message);

        program.Name = request.Name.Trim();
        program.Code = NormalizeCode(request.Code);
        program.IsActive = request.IsActive;
        program.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);

        return Result<ProgramConfigurationDto>.Success(Map(program));
    }

    public async Task<Result<ProgramCarrierDto>> AddCarrierAsync(Guid programId, UpsertProgramCarrierRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierDto>.Failure(validation.Value.Code, validation.Value.Message);

        var programExists = await _db.Set<ProgramConfiguration>().AnyAsync(p => p.Id == programId, ct);
        if (!programExists)
            return Result<ProgramCarrierDto>.Failure("PROGRAM_NOT_FOUND", "Program was not found.");

        var carrier = await _db.Set<Carrier>().SingleOrDefaultAsync(c => c.Id == request.CarrierId, ct);
        if (carrier is null)
            return Result<ProgramCarrierDto>.Failure("CARRIER_NOT_FOUND", "Carrier was not found.");

        var duplicate = await _db.Set<ProgramCarrier>()
            .AnyAsync(c => c.ProgramConfigurationId == programId && c.CarrierId == request.CarrierId, ct);
        if (duplicate)
            return Result<ProgramCarrierDto>.Failure("PROGRAM_CARRIER_DUPLICATE", "Carrier is already configured for this program.");

        var programCarrier = new ProgramCarrier
        {
            ProgramConfigurationId = programId,
            CarrierId = request.CarrierId,
            IsActive = request.IsActive,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            Notes = TrimToNull(request.Notes),
            Carrier = carrier
        };

        _db.Set<ProgramCarrier>().Add(programCarrier);
        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierDto>.Success(Map(programCarrier));
    }

    public async Task<Result<ProgramCarrierDto>> UpdateCarrierAsync(Guid programId, Guid programCarrierId, UpsertProgramCarrierRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierDto>.Failure(validation.Value.Code, validation.Value.Message);

        var programCarrier = await _db.Set<ProgramCarrier>()
            .Include(c => c.Carrier)
            .Include(c => c.LinesOfBusiness)
                .ThenInclude(l => l.States)
            .SingleOrDefaultAsync(c => c.Id == programCarrierId && c.ProgramConfigurationId == programId, ct);
        if (programCarrier is null)
            return Result<ProgramCarrierDto>.Failure("PROGRAM_CARRIER_NOT_FOUND", "Program carrier setup was not found.");

        var carrier = await _db.Set<Carrier>().SingleOrDefaultAsync(c => c.Id == request.CarrierId, ct);
        if (carrier is null)
            return Result<ProgramCarrierDto>.Failure("CARRIER_NOT_FOUND", "Carrier was not found.");

        var duplicate = await _db.Set<ProgramCarrier>()
            .AnyAsync(c => c.ProgramConfigurationId == programId && c.CarrierId == request.CarrierId && c.Id != programCarrierId, ct);
        if (duplicate)
            return Result<ProgramCarrierDto>.Failure("PROGRAM_CARRIER_DUPLICATE", "Carrier is already configured for this program.");

        programCarrier.CarrierId = request.CarrierId;
        programCarrier.Carrier = carrier;
        programCarrier.IsActive = request.IsActive;
        programCarrier.EffectiveDate = request.EffectiveDate;
        programCarrier.ExpirationDate = request.ExpirationDate;
        programCarrier.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierDto>.Success(Map(programCarrier));
    }

    public async Task<Result<ProgramCarrierLineOfBusinessDto>> AddLineOfBusinessAsync(Guid programId, Guid programCarrierId, UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure(validation.Value.Code, validation.Value.Message);
        var paymentValidation = ValidatePaymentTerms(request.PaymentTermsDays);
        if (paymentValidation is not null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure(paymentValidation.Value.Code, paymentValidation.Value.Message);

        var programCarrier = await _db.Set<ProgramCarrier>()
            .SingleOrDefaultAsync(c => c.Id == programCarrierId && c.ProgramConfigurationId == programId, ct);
        if (programCarrier is null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure("PROGRAM_CARRIER_NOT_FOUND", "Program carrier setup was not found.");

        var duplicate = await _db.Set<ProgramCarrierLineOfBusiness>()
            .AnyAsync(l => l.ProgramCarrierId == programCarrierId && l.LineOfBusiness == request.LineOfBusiness, ct);
        if (duplicate)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure("PROGRAM_CARRIER_LOB_DUPLICATE", "Line of business is already configured for this program carrier.");

        var lob = new ProgramCarrierLineOfBusiness
        {
            ProgramCarrierId = programCarrierId,
            LineOfBusiness = request.LineOfBusiness,
            IsActive = request.IsActive,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            BillingMode = TrimToNull(request.BillingMode),
            PaymentTermsDays = request.PaymentTermsDays,
            LondonUmr = TrimToNull(request.LondonUmr),
            LondonSectionNumber = TrimToNull(request.LondonSectionNumber),
            LondonClassOfBusiness = TrimToNull(request.LondonClassOfBusiness),
            LondonRiskCode = TrimToNull(request.LondonRiskCode),
            LondonInsuranceType = TrimToNull(request.LondonInsuranceType),
            Notes = TrimToNull(request.Notes)
        };

        _db.Set<ProgramCarrierLineOfBusiness>().Add(lob);
        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierLineOfBusinessDto>.Success(Map(lob));
    }

    public async Task<Result<ProgramCarrierLineOfBusinessDto>> UpdateLineOfBusinessAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, UpsertProgramCarrierLineOfBusinessRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure(validation.Value.Code, validation.Value.Message);
        var paymentValidation = ValidatePaymentTerms(request.PaymentTermsDays);
        if (paymentValidation is not null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure(paymentValidation.Value.Code, paymentValidation.Value.Message);

        var lob = await _db.Set<ProgramCarrierLineOfBusiness>()
            .Include(l => l.ProgramCarrier)
            .Include(l => l.States)
            .SingleOrDefaultAsync(l =>
                l.Id == programCarrierLobId &&
                l.ProgramCarrierId == programCarrierId &&
                l.ProgramCarrier.ProgramConfigurationId == programId, ct);
        if (lob is null)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure("PROGRAM_CARRIER_LOB_NOT_FOUND", "Program carrier line of business setup was not found.");

        var duplicate = await _db.Set<ProgramCarrierLineOfBusiness>()
            .AnyAsync(l => l.ProgramCarrierId == programCarrierId && l.LineOfBusiness == request.LineOfBusiness && l.Id != programCarrierLobId, ct);
        if (duplicate)
            return Result<ProgramCarrierLineOfBusinessDto>.Failure("PROGRAM_CARRIER_LOB_DUPLICATE", "Line of business is already configured for this program carrier.");

        lob.LineOfBusiness = request.LineOfBusiness;
        lob.IsActive = request.IsActive;
        lob.EffectiveDate = request.EffectiveDate;
        lob.ExpirationDate = request.ExpirationDate;
        lob.BillingMode = TrimToNull(request.BillingMode);
        lob.PaymentTermsDays = request.PaymentTermsDays;
        lob.LondonUmr = TrimToNull(request.LondonUmr);
        lob.LondonSectionNumber = TrimToNull(request.LondonSectionNumber);
        lob.LondonClassOfBusiness = TrimToNull(request.LondonClassOfBusiness);
        lob.LondonRiskCode = TrimToNull(request.LondonRiskCode);
        lob.LondonInsuranceType = TrimToNull(request.LondonInsuranceType);
        lob.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierLineOfBusinessDto>.Success(Map(lob));
    }

    public async Task<Result<ProgramCarrierLobStateDto>> AddStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, UpsertProgramCarrierLobStateRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierLobStateDto>.Failure(validation.Value.Code, validation.Value.Message);

        var stateValidation = NormalizeStateCode(request.StateCode);
        if (!stateValidation.IsSuccess)
            return Result<ProgramCarrierLobStateDto>.Failure(stateValidation.ErrorCode!, stateValidation.ErrorMessage!);

        var lob = await _db.Set<ProgramCarrierLineOfBusiness>()
            .Include(l => l.ProgramCarrier)
            .SingleOrDefaultAsync(l =>
                l.Id == programCarrierLobId &&
                l.ProgramCarrierId == programCarrierId &&
                l.ProgramCarrier.ProgramConfigurationId == programId, ct);
        if (lob is null)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_NOT_FOUND", "Program carrier line of business setup was not found.");

        var stateCode = stateValidation.Value!;
        var duplicate = await _db.Set<ProgramCarrierLobState>()
            .AnyAsync(s => s.ProgramCarrierLineOfBusinessId == programCarrierLobId && s.StateCode == stateCode, ct);
        if (duplicate)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_DUPLICATE", "State is already configured for this program carrier line of business.");

        var state = new ProgramCarrierLobState
        {
            ProgramCarrierLineOfBusinessId = programCarrierLobId,
            StateCode = stateCode,
            IsActive = request.IsActive,
            EffectiveDate = request.EffectiveDate,
            ExpirationDate = request.ExpirationDate,
            Notes = TrimToNull(request.Notes)
        };

        _db.Set<ProgramCarrierLobState>().Add(state);
        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierLobStateDto>.Success(Map(state));
    }

    public async Task<Result<ProgramCarrierLobStateDto>> UpdateStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, Guid stateId, UpsertProgramCarrierLobStateRequest request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.EffectiveDate, request.ExpirationDate);
        if (validation is not null)
            return Result<ProgramCarrierLobStateDto>.Failure(validation.Value.Code, validation.Value.Message);

        var stateValidation = NormalizeStateCode(request.StateCode);
        if (!stateValidation.IsSuccess)
            return Result<ProgramCarrierLobStateDto>.Failure(stateValidation.ErrorCode!, stateValidation.ErrorMessage!);

        var state = await _db.Set<ProgramCarrierLobState>()
            .Include(s => s.ProgramCarrierLineOfBusiness)
                .ThenInclude(l => l.ProgramCarrier)
            .SingleOrDefaultAsync(s =>
                s.Id == stateId &&
                s.ProgramCarrierLineOfBusinessId == programCarrierLobId &&
                s.ProgramCarrierLineOfBusiness.ProgramCarrierId == programCarrierId &&
                s.ProgramCarrierLineOfBusiness.ProgramCarrier.ProgramConfigurationId == programId, ct);
        if (state is null)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_NOT_FOUND", "Program carrier state setup was not found.");

        var stateCode = stateValidation.Value!;
        var duplicate = await _db.Set<ProgramCarrierLobState>()
            .AnyAsync(s => s.ProgramCarrierLineOfBusinessId == programCarrierLobId && s.StateCode == stateCode && s.Id != stateId, ct);
        if (duplicate)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_DUPLICATE", "State is already configured for this program carrier line of business.");

        state.StateCode = stateCode;
        state.IsActive = request.IsActive;
        state.EffectiveDate = request.EffectiveDate;
        state.ExpirationDate = request.ExpirationDate;
        state.Notes = TrimToNull(request.Notes);

        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierLobStateDto>.Success(Map(state));
    }

    public async Task<Result<ProgramCarrierLobStateDto>> CopyStateAsync(Guid programId, Guid programCarrierId, Guid programCarrierLobId, CopyProgramCarrierLobStateRequest request, CancellationToken ct = default)
    {
        var sourceValidation = NormalizeStateCode(request.SourceStateCode);
        if (!sourceValidation.IsSuccess)
            return Result<ProgramCarrierLobStateDto>.Failure(sourceValidation.ErrorCode!, sourceValidation.ErrorMessage!);

        var targetValidation = NormalizeStateCode(request.TargetStateCode);
        if (!targetValidation.IsSuccess)
            return Result<ProgramCarrierLobStateDto>.Failure(targetValidation.ErrorCode!, targetValidation.ErrorMessage!);

        var sourceState = sourceValidation.Value!;
        var targetState = targetValidation.Value!;
        if (sourceState == targetState)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_COPY_SAME_STATE", "Source and target states must be different.");

        var source = await _db.Set<ProgramCarrierLobState>()
            .Include(s => s.ProgramCarrierLineOfBusiness)
                .ThenInclude(l => l.ProgramCarrier)
            .SingleOrDefaultAsync(s =>
                s.ProgramCarrierLineOfBusinessId == programCarrierLobId &&
                s.StateCode == sourceState &&
                s.ProgramCarrierLineOfBusiness.ProgramCarrierId == programCarrierId &&
                s.ProgramCarrierLineOfBusiness.ProgramCarrier.ProgramConfigurationId == programId, ct);
        if (source is null)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_NOT_FOUND", "Source state setup was not found.");

        var duplicate = await _db.Set<ProgramCarrierLobState>()
            .AnyAsync(s => s.ProgramCarrierLineOfBusinessId == programCarrierLobId && s.StateCode == targetState, ct);
        if (duplicate)
            return Result<ProgramCarrierLobStateDto>.Failure("PROGRAM_CARRIER_LOB_STATE_DUPLICATE", "Target state is already configured for this program carrier line of business.");

        var copy = new ProgramCarrierLobState
        {
            ProgramCarrierLineOfBusinessId = programCarrierLobId,
            StateCode = targetState,
            IsActive = source.IsActive,
            EffectiveDate = source.EffectiveDate,
            ExpirationDate = source.ExpirationDate,
            Notes = source.Notes
        };

        _db.Set<ProgramCarrierLobState>().Add(copy);
        await _db.SaveChangesAsync(ct);
        await CopyStatePolicyPackagesAsync(programId, programCarrierId, source.ProgramCarrierLineOfBusiness.LineOfBusiness, sourceState, targetState, copy.Id, ct);
        await CopyStateProposalDocumentsAsync(programId, programCarrierId, source.ProgramCarrierLineOfBusiness.LineOfBusiness, sourceState, targetState, copy.Id, ct);
        await _db.SaveChangesAsync(ct);

        return Result<ProgramCarrierLobStateDto>.Success(Map(copy));
    }

    public async Task<ProgramOrphanAuditDto> GetOrphanAuditAsync(CancellationToken ct = default)
    {
        var programs = await _db.Set<ProgramConfiguration>()
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.Carrier)
            .Include(p => p.ProgramCarriers)
                .ThenInclude(c => c.LinesOfBusiness)
                    .ThenInclude(l => l.States)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var issues = new List<ProgramOrphanIssueDto>();

        foreach (var program in programs)
        {
            var programPath = program.Code;
            var activeCarriers = program.ProgramCarriers.Where(c => c.IsActive).ToList();

            if (!program.ProgramCarriers.Any())
            {
                issues.Add(new ProgramOrphanIssueDto("error", programPath, "Program has no carriers configured."));
                continue;
            }

            if (activeCarriers.Count == 0)
                issues.Add(new ProgramOrphanIssueDto("warning", programPath, "Program has no active carriers."));

            foreach (var carrier in program.ProgramCarriers)
            {
                var carrierPath = $"{programPath} / {carrier.Carrier?.Name ?? carrier.CarrierId.ToString()}";
                var activeLobs = carrier.LinesOfBusiness.Where(l => l.IsActive).ToList();

                if (!carrier.LinesOfBusiness.Any())
                {
                    issues.Add(new ProgramOrphanIssueDto("error", carrierPath, "Carrier has no lines of business configured."));
                    continue;
                }

                if (activeLobs.Count == 0)
                    issues.Add(new ProgramOrphanIssueDto("warning", carrierPath, "Carrier has no active lines of business."));

                foreach (var lob in carrier.LinesOfBusiness)
                {
                    var lobPath = $"{carrierPath} / {GetLobLabel(lob.LineOfBusiness)}";
                    var activeStates = lob.States.Where(s => s.IsActive).ToList();

                    if (!lob.States.Any())
                    {
                        issues.Add(new ProgramOrphanIssueDto("error", lobPath, "Line of business has no states configured."));
                        continue;
                    }

                    if (activeStates.Count == 0)
                        issues.Add(new ProgramOrphanIssueDto("warning", lobPath, "Line of business has no active states."));
                }
            }
        }

        return new ProgramOrphanAuditDto(issues);
    }

    private async Task CopyStatePolicyPackagesAsync(
        Guid programId,
        Guid programCarrierId,
        PolicyLineOfBusiness lineOfBusiness,
        string sourceState,
        string targetState,
        Guid targetProgramCarrierLobStateId,
        CancellationToken ct)
    {
        var carrierId = await _db.Set<ProgramCarrier>()
            .Where(c => c.Id == programCarrierId && c.ProgramConfigurationId == programId)
            .Select(c => c.CarrierId)
            .SingleAsync(ct);
        var sourcePackages = await _db.Set<PolicyPackageConfiguration>()
            .Include(p => p.Forms)
            .Where(p =>
                p.ProgramConfigurationId == programId &&
                p.CarrierId == carrierId &&
                p.LineOfBusiness == lineOfBusiness &&
                p.State == sourceState &&
                !p.IsDeleted)
            .ToListAsync(ct);

        foreach (var sourcePackage in sourcePackages)
        {
            _db.Set<PolicyPackageConfiguration>().Add(new PolicyPackageConfiguration
            {
                ProgramConfigurationId = sourcePackage.ProgramConfigurationId,
                CarrierId = sourcePackage.CarrierId,
                LineOfBusiness = sourcePackage.LineOfBusiness,
                State = targetState,
                ProgramCarrierLineOfBusinessId = null,
                ProgramCarrierLobStateId = targetProgramCarrierLobStateId,
                Name = ReplaceStateToken(sourcePackage.Name, sourceState, targetState),
                IsActive = sourcePackage.IsActive,
                Forms = sourcePackage.Forms
                    .Where(f => !f.IsDeleted)
                    .OrderBy(f => f.SequenceOrder)
                    .Select(f => new PolicyPackageForm
                    {
                        PolicyFormTemplateId = f.PolicyFormTemplateId,
                        SequenceOrder = f.SequenceOrder,
                        FormType = f.FormType,
                        TriggerConditionJson = f.TriggerConditionJson,
                        Notes = f.Notes,
                    })
                    .ToList(),
            });
        }
    }

    private async Task CopyStateProposalDocumentsAsync(
        Guid programId,
        Guid programCarrierId,
        PolicyLineOfBusiness lineOfBusiness,
        string sourceState,
        string targetState,
        Guid targetProgramCarrierLobStateId,
        CancellationToken ct)
    {
        var carrierId = await _db.Set<ProgramCarrier>()
            .Where(c => c.Id == programCarrierId && c.ProgramConfigurationId == programId)
            .Select(c => c.CarrierId)
            .SingleAsync(ct);
        var sourceDocuments = await _db.Set<ProposalDocumentConfiguration>()
            .Where(p =>
                p.ProgramConfigurationId == programId &&
                p.CarrierId == carrierId &&
                p.LineOfBusiness == lineOfBusiness &&
                p.State == sourceState &&
                !p.IsDeleted)
            .ToListAsync(ct);

        foreach (var sourceDocument in sourceDocuments)
        {
            _db.Set<ProposalDocumentConfiguration>().Add(new ProposalDocumentConfiguration
            {
                ProgramConfigurationId = sourceDocument.ProgramConfigurationId,
                CarrierId = sourceDocument.CarrierId,
                LineOfBusiness = sourceDocument.LineOfBusiness,
                State = targetState,
                ProgramCarrierLineOfBusinessId = null,
                ProgramCarrierLobStateId = targetProgramCarrierLobStateId,
                Role = sourceDocument.Role,
                DocumentTemplateId = sourceDocument.DocumentTemplateId,
                SequenceOrder = sourceDocument.SequenceOrder,
                IsActive = sourceDocument.IsActive,
                EffectiveDate = sourceDocument.EffectiveDate,
                ExpirationDate = sourceDocument.ExpirationDate,
                Notes = sourceDocument.Notes,
            });
        }
    }

    private async Task<(string Code, string Message)?> ValidateAsync(Guid? existingId, string name, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ("PROGRAM_NAME_REQUIRED", "Program name is required.");
        if (string.IsNullOrWhiteSpace(code))
            return ("PROGRAM_CODE_REQUIRED", "Program code is required.");

        var normalizedCode = NormalizeCode(code);
        var duplicateCode = await _db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Code == normalizedCode && (!existingId.HasValue || p.Id != existingId.Value), ct);
        if (duplicateCode)
            return ("PROGRAM_CODE_DUPLICATE", "Program code is already in use.");

        return null;
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();
    private static string ReplaceStateToken(string name, string sourceState, string targetState) =>
        string.IsNullOrWhiteSpace(name)
            ? name
            : name.Replace(sourceState, targetState, StringComparison.OrdinalIgnoreCase);
    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static (string Code, string Message)? ValidateDates(DateOnly effectiveDate, DateOnly? expirationDate) =>
        expirationDate.HasValue && expirationDate.Value < effectiveDate
            ? ("INVALID_DATE_RANGE", "Expiration date cannot be before effective date.")
            : null;
    private static (string Code, string Message)? ValidatePaymentTerms(int? paymentTermsDays) =>
        paymentTermsDays is < 0 or > 365
            ? ("INVALID_PAYMENT_TERMS", "Payment terms must be between 0 and 365 days.")
            : null;

    private static Result<string> NormalizeStateCode(string stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return Result<string>.Failure("STATE_CODE_REQUIRED", "State code is required.");

        var normalized = stateCode.Trim().ToUpperInvariant();
        return normalized.Length == 2
            ? Result<string>.Success(normalized)
            : Result<string>.Failure("STATE_CODE_INVALID", "State code must be two characters.");
    }

    private static ProgramConfigurationDto Map(ProgramConfiguration program) =>
        new(
            program.Id,
            program.Name,
            program.Code,
            program.IsActive,
            program.Notes,
            program.CreatedAt,
            program.UpdatedAt,
            program.ProgramCarriers
                .OrderBy(c => c.Carrier.Name)
                .Select(Map)
                .ToList());

    private static ProgramCarrierDto Map(ProgramCarrier programCarrier) =>
        new(
            programCarrier.Id,
            programCarrier.ProgramConfigurationId,
            programCarrier.CarrierId,
            programCarrier.Carrier?.Name ?? string.Empty,
            programCarrier.IsActive,
            programCarrier.EffectiveDate,
            programCarrier.ExpirationDate,
            programCarrier.Notes,
            programCarrier.LinesOfBusiness
                .OrderBy(l => l.LineOfBusiness)
                .Select(Map)
                .ToList());

    private static ProgramCarrierLineOfBusinessDto Map(ProgramCarrierLineOfBusiness lob) =>
        new(
            lob.Id,
            lob.ProgramCarrierId,
            lob.LineOfBusiness,
            GetLobLabel(lob.LineOfBusiness),
            lob.IsActive,
            lob.EffectiveDate,
            lob.ExpirationDate,
            lob.Notes,
            lob.BillingMode,
            lob.PaymentTermsDays,
            lob.LondonUmr,
            lob.LondonSectionNumber,
            lob.LondonClassOfBusiness,
            lob.LondonRiskCode,
            lob.LondonInsuranceType,
            lob.States
                .OrderBy(s => s.StateCode)
                .Select(Map)
                .ToList());

    private static ProgramCarrierLobStateDto Map(ProgramCarrierLobState state) =>
        new(
            state.Id,
            state.ProgramCarrierLineOfBusinessId,
            state.StateCode,
            state.IsActive,
            state.EffectiveDate,
            state.ExpirationDate,
            state.Notes);

    private static string GetLobLabel(PolicyLineOfBusiness lob) => lob switch
    {
        PolicyLineOfBusiness.GeneralLiability => "General Liability",
        PolicyLineOfBusiness.InlandMarine => "Inland Marine",
        PolicyLineOfBusiness.AutoLiability => "Auto Liability",
        PolicyLineOfBusiness.AutoPhysicalDamage => "Auto Physical Damage",
        _ => lob.ToString()
    };
}
