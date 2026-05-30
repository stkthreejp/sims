using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class FeeAdminService : IFeeAdminService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    private sealed record ResolvedFeeProgramScope(
        Guid? ProgramCarrierId,
        Guid? ProgramCarrierLineOfBusinessId,
        Guid? ProgramCarrierLobStateId,
        string? LineOfBusiness,
        string? StateCode);

    public FeeAdminService(IServiceProvider sp) => _sp = sp;

    public async Task<IReadOnlyList<FeeDefinitionDto>> GetDefinitionsAsync(CancellationToken ct = default)
    {
        var defs = await Db.Set<FeeDefinition>()
            .OrderBy(d => d.CalculationOrder).ThenBy(d => d.DisplayName)
            .ToListAsync(ct);
        return defs.Select(MapDefinition).ToList();
    }

    public async Task<Result<FeeDefinitionDto>> GetDefinitionAsync(long id, CancellationToken ct = default)
    {
        var def = await Db.Set<FeeDefinition>().FindAsync([id], ct);
        return def is null ? Result<FeeDefinitionDto>.Failure("NOT_FOUND", "Fee definition not found") : Result<FeeDefinitionDto>.Success(MapDefinition(def));
    }

    public async Task<Result<FeeDefinitionDto>> CreateDefinitionAsync(CreateFeeDefinitionRequest req, CancellationToken ct = default)
    {
        var def = new FeeDefinition
        {
            Code = req.Code,
            DisplayName = req.DisplayName,
            FeeCategory = req.FeeCategory,
            IsTaxable = req.IsTaxable,
            CalculationOrder = req.CalculationOrder,
            LedgerAccountId = req.LedgerAccountId,
            CreatedAt = DateTime.UtcNow
        };
        Db.Set<FeeDefinition>().Add(def);
        await Db.SaveChangesAsync(ct);
        return Result<FeeDefinitionDto>.Success(MapDefinition(def));
    }

    public async Task<IReadOnlyList<FeeRuleVersionDto>> GetVersionsAsync(long feeDefinitionId, CancellationToken ct = default)
    {
        var versions = await Db.Set<FeeRuleVersion>()
            .Include(v => v.FeeDefinition)
            .Include(v => v.ProgramConfiguration)
            .Include(v => v.PremiumBrackets)
            .Where(v => v.FeeDefinitionId == feeDefinitionId)
            .OrderByDescending(v => v.EffectiveDate)
            .ToListAsync(ct);

        var nonTaxableMap = await GetNonTaxableMapAsync(
            versions.Select(v => v.FeeDefinitionId).Distinct().ToList(), ct);

        return versions.Select(v => MapVersion(v, nonTaxableMap)).ToList();
    }

    public async Task<Result<FeeRuleVersionDto>> GetVersionAsync(long id, CancellationToken ct = default)
    {
        var version = await Db.Set<FeeRuleVersion>()
            .Include(v => v.FeeDefinition)
            .Include(v => v.ProgramConfiguration)
            .Include(v => v.PremiumBrackets)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (version is null) return Result<FeeRuleVersionDto>.Failure("NOT_FOUND", "Fee rule version not found");

        var nonTaxableMap = await GetNonTaxableMapAsync([version.FeeDefinitionId], ct);
        return Result<FeeRuleVersionDto>.Success(MapVersion(version, nonTaxableMap));
    }

    public async Task<Result<FeeRuleVersionDto>> CreateVersionAsync(Guid userId, CreateFeeRuleVersionRequest req, CancellationToken ct = default)
    {
        var validation = await ValidateVersionRequestAsync(req, ct);
        if (!validation.IsSuccess)
            return Result<FeeRuleVersionDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        var version = BuildVersion(req, userId, validation.Value!);
        Db.Set<FeeRuleVersion>().Add(version);

        var auditLog = new FeeAuditLog
        {
            FeeRuleVersionId = version.Id,
            EditedBy = userId,
            EditedAt = DateTime.UtcNow,
            ChangeType = "Created"
        };

        // We need the version saved first to get its Id for the audit log
        await Db.SaveChangesAsync(ct);

        auditLog.FeeRuleVersionId = version.Id;
        Db.Set<FeeAuditLog>().Add(auditLog);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(version).Reference(v => v.FeeDefinition).LoadAsync(ct);
        if (version.ProgramConfigurationId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramConfiguration).LoadAsync(ct);
        if (version.ProgramCarrierId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrier).LoadAsync(ct);
        if (version.ProgramCarrierLineOfBusinessId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrierLineOfBusiness).LoadAsync(ct);
        if (version.ProgramCarrierLobStateId.HasValue)
            await Db.Entry(version).Reference(v => v.ProgramCarrierLobState).LoadAsync(ct);
        var nonTaxableMap = await GetNonTaxableMapAsync([version.FeeDefinitionId], ct);
        return Result<FeeRuleVersionDto>.Success(MapVersion(version, nonTaxableMap));
    }

    public async Task<Result<FeeRuleVersionDto>> NewVersionFromExistingAsync(Guid userId, long existingVersionId, CreateFeeRuleVersionRequest req, CancellationToken ct = default)
    {
        var existing = await Db.Set<FeeRuleVersion>().FindAsync([existingVersionId], ct);
        if (existing is null) return Result<FeeRuleVersionDto>.Failure("NOT_FOUND", "Existing version not found");

        var validation = await ValidateVersionRequestAsync(req, ct);
        if (!validation.IsSuccess)
            return Result<FeeRuleVersionDto>.Failure(validation.ErrorCode!, validation.ErrorMessage!);

        // Stamp old version's disabled_date with the new version's effective_date in one transaction
        existing.DisabledDate = req.EffectiveDate;

        var newVersion = BuildVersion(req, userId, validation.Value!);
        Db.Set<FeeRuleVersion>().Add(newVersion);

        await Db.SaveChangesAsync(ct);

        Db.Set<FeeAuditLog>().Add(new FeeAuditLog
        {
            FeeRuleVersionId = newVersion.Id,
            EditedBy = userId,
            EditedAt = DateTime.UtcNow,
            ChangeType = "NewVersion",
            Notes = $"Supersedes version {existingVersionId}"
        });
        await Db.SaveChangesAsync(ct);

        await Db.Entry(newVersion).Reference(v => v.FeeDefinition).LoadAsync(ct);
        if (newVersion.ProgramConfigurationId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramConfiguration).LoadAsync(ct);
        if (newVersion.ProgramCarrierId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrier).LoadAsync(ct);
        if (newVersion.ProgramCarrierLineOfBusinessId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrierLineOfBusiness).LoadAsync(ct);
        if (newVersion.ProgramCarrierLobStateId.HasValue)
            await Db.Entry(newVersion).Reference(v => v.ProgramCarrierLobState).LoadAsync(ct);
        var nonTaxableMap = await GetNonTaxableMapAsync([newVersion.FeeDefinitionId], ct);
        return Result<FeeRuleVersionDto>.Success(MapVersion(newVersion, nonTaxableMap));
    }

    public async Task<Result> DisableVersionAsync(Guid userId, long id, DateOnly disabledDate, string? notes, CancellationToken ct = default)
    {
        var version = await Db.Set<FeeRuleVersion>().FindAsync([id], ct);
        if (version is null) return Result.Failure("ERROR", "Fee rule version not found");

        version.DisabledDate = disabledDate;
        version.LastEditedBy = userId;
        version.LastEditedAt = DateTime.UtcNow;

        Db.Set<FeeAuditLog>().Add(new FeeAuditLog
        {
            FeeRuleVersionId = id,
            EditedBy = userId,
            EditedAt = DateTime.UtcNow,
            ChangeType = "Disabled",
            Notes = notes
        });

        await Db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetStateTaxabilityAsync(long feeDefinitionId, SetStateTaxabilityRequest req, CancellationToken ct = default)
    {
        var db = Db;
        var existing = await db.Set<FeeStateTaxability>()
            .Where(s => s.FeeDefinitionId == feeDefinitionId)
            .ToListAsync(ct);

        db.Set<FeeStateTaxability>().RemoveRange(existing);

        foreach (var code in req.NonTaxableStateCodes)
        {
            db.Set<FeeStateTaxability>().Add(new FeeStateTaxability
            {
                FeeDefinitionId = feeDefinitionId,
                StateCode = code,
                IsTaxable = false
            });
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<IReadOnlyList<FeeAuditLogDto>> GetAuditLogAsync(long feeRuleVersionId, CancellationToken ct = default)
    {
        var logs = await Db.Set<FeeAuditLog>()
            .Where(l => l.FeeRuleVersionId == feeRuleVersionId)
            .OrderByDescending(l => l.EditedAt)
            .ToListAsync(ct);

        return logs.Select(l => new FeeAuditLogDto(l.Id, l.EditedBy, l.EditedAt, l.ChangeType, l.FieldChanges, l.Notes)).ToList();
    }

    // --- helpers ---

    private static FeeDefinitionDto MapDefinition(FeeDefinition d) =>
        new(d.Id, d.Code, d.DisplayName, d.FeeCategory, d.IsTaxable, d.CalculationOrder, d.LedgerAccountId);

    private static FeeRuleVersionDto MapVersion(FeeRuleVersion v, Dictionary<long, List<string>> nonTaxableMap) =>
        new(
            Id: v.Id,
            FeeDefinitionId: v.FeeDefinitionId,
            FeeCode: v.FeeDefinition?.Code ?? string.Empty,
            FeeDisplayName: v.FeeDefinition?.DisplayName ?? string.Empty,
            ProgramConfigurationId: v.ProgramConfigurationId,
            ProgramName: v.ProgramConfiguration?.Name,
            CarrierId: v.CarrierId,
            CompanyId: v.CompanyId,
            ProducerId: v.ProducerId,
            LineOfBusiness: v.LineOfBusiness,
            StateCode: v.StateCode,
            City: v.City,
            LicenseType: v.LicenseType,
            EffectiveDate: v.EffectiveDate,
            DisabledDate: v.DisabledDate,
            CalcType: v.CalcType,
            FlatAmount: v.FlatAmount,
            PercentRate: v.PercentRate,
            PercentOfNet: v.PercentOfNet,
            MinimumAmount: v.MinimumAmount,
            MaxPercent: v.MaxPercent,
            MaxAmount: v.MaxAmount,
            Commissionable: v.Commissionable,
            InstallmentBehavior: v.InstallmentBehavior,
            SplitByParticipation: v.SplitByParticipation,
            FullyEarned: v.FullyEarned,
            FullyEarnedDays: v.FullyEarnedDays,
            ExcludeTerrorism: v.ExcludeTerrorism,
            MultiplyByLocations: v.MultiplyByLocations,
            MultiplyByVehicles: v.MultiplyByVehicles,
            SendToAccounting: v.SendToAccounting,
            ApplyOnlyOnce: v.ApplyOnlyOnce,
            MandatoryCharge: v.MandatoryCharge,
            ApplyAutomatically: v.ApplyAutomatically,
            ApplyWhenPackagePolicyOnly: v.ApplyWhenPackagePolicyOnly,
            DoNotApplyWhenPackagePolicyOnly: v.DoNotApplyWhenPackagePolicyOnly,
            ApplyToChildLines: v.ApplyToChildLines,
            OnlyAppliesToIssuanceState: v.OnlyAppliesToIssuanceState,
            AppliesToFlatCancellations: v.AppliesToFlatCancellations,
            PremiumMinThreshold: v.PremiumMinThreshold,
            PremiumMaxThreshold: v.PremiumMaxThreshold,
            PremiumThresholdBasis: v.PremiumThresholdBasis,
            StateCountMin: v.StateCountMin,
            StateCountMax: v.StateCountMax,
            RoundingMode: v.RoundingMode,
            ExcludeWhenNotFiling: v.ExcludeWhenNotFiling,
            ExcludeOnEndorsements: v.ExcludeOnEndorsements,
            ExcludeOnRenewal: v.ExcludeOnRenewal,
            ExcludeOnOriginalBinder: v.ExcludeOnOriginalBinder,
            ExcludeOnMultiCarrierPolicy: v.ExcludeOnMultiCarrierPolicy,
            PayHomeState: v.PayHomeState,
            ExcludedPolicyTransactionTypes: v.ExcludedPolicyTransactionTypes,
            PayableRouting: v.PayableRouting,
            PayablePayeeId: v.PayablePayeeId,
            MasterPayeeWhenHomeState: v.MasterPayeeWhenHomeState,
            Notes: v.Notes,
            PremiumBrackets: v.PremiumBrackets
                .OrderBy(b => b.TierFrom)
                .Select(b => new FeePremiumBracketDto(b.Id, b.TierFrom, b.TierTo, b.PercentRate))
                .ToList(),
            NonTaxableStates: nonTaxableMap.TryGetValue(v.FeeDefinitionId, out var states) ? states : []
        );

    private async Task<Dictionary<long, List<string>>> GetNonTaxableMapAsync(
        IEnumerable<long> feeDefIds, CancellationToken ct)
    {
        var rows = await Db.Set<FeeStateTaxability>()
            .Where(s => feeDefIds.Contains(s.FeeDefinitionId) && !s.IsTaxable)
            .ToListAsync(ct);

        return rows
            .GroupBy(s => s.FeeDefinitionId)
            .ToDictionary(g => g.Key, g => g.Select(s => s.StateCode).ToList());
    }

    private async Task<Result<ResolvedFeeProgramScope>> ValidateVersionRequestAsync(
        CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        if (req.PayableRouting is not "NotPayable" and not "Company" and not "Entity")
            return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_ROUTING_INVALID", "Payable routing must be NotPayable, Company, or Entity.");

        if (req.PayableRouting == "Entity")
        {
            if (!req.PayablePayeeId.HasValue)
                return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_PAYEE_REQUIRED", "A third-party/vendor payee is required when payable routing is Entity.");

            var payeeExists = await Db.Set<Payee>()
                .AnyAsync(p => p.Id == req.PayablePayeeId.Value && p.IsActive, ct);
            if (!payeeExists)
                return Result<ResolvedFeeProgramScope>.Failure("PAYABLE_PAYEE_NOT_FOUND", "The selected third-party/vendor payee was not found or is inactive.");
        }

        return await ResolveProgramScopeAsync(req, ct);
    }

    private async Task<Result<ResolvedFeeProgramScope>> ResolveProgramScopeAsync(
        CreateFeeRuleVersionRequest req, CancellationToken ct)
    {
        var normalizedState = NormalizeStateCode(req.StateCode);
        if (!string.IsNullOrWhiteSpace(req.StateCode) && normalizedState is null)
            return Result<ResolvedFeeProgramScope>.Failure("STATE_CODE_INVALID", "State code must be two characters.");

        var normalizedLob = NormalizeLineOfBusiness(req.LineOfBusiness);
        PolicyLineOfBusiness? parsedLob = null;
        if (!string.IsNullOrWhiteSpace(normalizedLob))
        {
            var lobName = Enum.GetNames<PolicyLineOfBusiness>()
                .FirstOrDefault(name => string.Equals(name, normalizedLob, StringComparison.OrdinalIgnoreCase));
            if (lobName is null)
                return Result<ResolvedFeeProgramScope>.Failure("LOB_INVALID", "Line of business is not valid.");

            parsedLob = Enum.Parse<PolicyLineOfBusiness>(lobName);
            normalizedLob = lobName;
        }

        if (!req.ProgramConfigurationId.HasValue)
            return Result<ResolvedFeeProgramScope>.Success(new(null, null, null, normalizedLob, normalizedState));

        var programExists = await Db.Set<ProgramConfiguration>()
            .AnyAsync(p => p.Id == req.ProgramConfigurationId.Value && p.IsActive, ct);
        if (!programExists)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_NOT_FOUND", "The selected Program was not found or is inactive.");

        if (!req.CarrierId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(normalizedLob) || normalizedState is not null)
                return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PARENT_REQUIRED", "Select a carrier before selecting a Program line of business or state.");

            return Result<ResolvedFeeProgramScope>.Success(new(null, null, null, null, null));
        }

        var programCarrier = await Db.Set<ProgramCarrier>()
            .FirstOrDefaultAsync(c =>
                c.ProgramConfigurationId == req.ProgramConfigurationId.Value &&
                c.CarrierId == req.CarrierId.Value &&
                c.IsActive &&
                c.EffectiveDate <= req.EffectiveDate &&
                (c.ExpirationDate == null || c.ExpirationDate >= req.EffectiveDate), ct);

        if (programCarrier is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected carrier is not active for this Program on the fee effective date.");

        if (string.IsNullOrWhiteSpace(normalizedLob))
        {
            if (normalizedState is not null)
                return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PARENT_REQUIRED", "Select a line of business before selecting a Program state.");

            return Result<ResolvedFeeProgramScope>.Success(new(programCarrier.Id, null, null, null, null));
        }

        var lob = parsedLob!.Value;

        var programLob = await Db.Set<ProgramCarrierLineOfBusiness>()
            .FirstOrDefaultAsync(l =>
                l.ProgramCarrierId == programCarrier.Id &&
                l.LineOfBusiness == lob &&
                l.IsActive &&
                l.EffectiveDate <= req.EffectiveDate &&
                (l.ExpirationDate == null || l.ExpirationDate >= req.EffectiveDate), ct);

        if (programLob is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected line of business is not active for this Program carrier on the fee effective date.");

        if (normalizedState is null)
            return Result<ResolvedFeeProgramScope>.Success(new(null, programLob.Id, null, normalizedLob, null));

        var programState = await Db.Set<ProgramCarrierLobState>()
            .FirstOrDefaultAsync(s =>
                s.ProgramCarrierLineOfBusinessId == programLob.Id &&
                s.StateCode == normalizedState &&
                s.IsActive &&
                s.EffectiveDate <= req.EffectiveDate &&
                (s.ExpirationDate == null || s.ExpirationDate >= req.EffectiveDate), ct);

        if (programState is null)
            return Result<ResolvedFeeProgramScope>.Failure("PROGRAM_SCOPE_PATH_NOT_FOUND", "The selected state is not active for this Program carrier and line of business on the fee effective date.");

        return Result<ResolvedFeeProgramScope>.Success(new(null, null, programState.Id, normalizedLob, normalizedState));
    }

    private static string? NormalizeStateCode(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return null;

        var normalized = stateCode.Trim().ToUpperInvariant();
        return normalized.Length == 2 ? normalized : null;
    }

    private static string? NormalizeLineOfBusiness(string? lineOfBusiness)
    {
        if (string.IsNullOrWhiteSpace(lineOfBusiness))
            return null;

        return lineOfBusiness.Trim();
    }

    private static FeeRuleVersion BuildVersion(CreateFeeRuleVersionRequest req, Guid userId, ResolvedFeeProgramScope scope)
    {
        var version = new FeeRuleVersion
        {
            FeeDefinitionId = req.FeeDefinitionId,
            ProgramConfigurationId = req.ProgramConfigurationId,
            CarrierId = req.CarrierId,
            CompanyId = req.CompanyId,
            ProducerId = req.ProducerId,
            LineOfBusiness = scope.LineOfBusiness,
            StateCode = scope.StateCode,
            ProgramCarrierId = scope.ProgramCarrierId,
            ProgramCarrierLineOfBusinessId = scope.ProgramCarrierLineOfBusinessId,
            ProgramCarrierLobStateId = scope.ProgramCarrierLobStateId,
            City = req.City,
            LicenseType = req.LicenseType,
            EffectiveDate = req.EffectiveDate,
            CalcType = req.CalcType,
            FlatAmount = req.FlatAmount,
            PercentRate = req.PercentRate,
            PercentOfNet = req.PercentOfNet,
            MinimumAmount = req.MinimumAmount,
            MaxPercent = req.MaxPercent,
            MaxAmount = req.MaxAmount,
            Commissionable = req.Commissionable,
            InstallmentBehavior = req.InstallmentBehavior,
            SplitByParticipation = req.SplitByParticipation,
            FullyEarned = req.FullyEarned,
            FullyEarnedDays = req.FullyEarnedDays,
            ExcludeTerrorism = req.ExcludeTerrorism,
            MultiplyByLocations = req.MultiplyByLocations,
            MultiplyByVehicles = req.MultiplyByVehicles,
            SendToAccounting = req.SendToAccounting,
            ApplyOnlyOnce = req.ApplyOnlyOnce,
            MandatoryCharge = req.MandatoryCharge,
            ApplyAutomatically = req.ApplyAutomatically,
            ApplyWhenPackagePolicyOnly = req.ApplyWhenPackagePolicyOnly,
            DoNotApplyWhenPackagePolicyOnly = req.DoNotApplyWhenPackagePolicyOnly,
            ApplyToChildLines = req.ApplyToChildLines,
            OnlyAppliesToIssuanceState = req.OnlyAppliesToIssuanceState,
            AppliesToFlatCancellations = req.AppliesToFlatCancellations,
            PremiumMinThreshold = req.PremiumMinThreshold,
            PremiumMaxThreshold = req.PremiumMaxThreshold,
            PremiumThresholdBasis = req.PremiumThresholdBasis,
            StateCountMin = req.StateCountMin,
            StateCountMax = req.StateCountMax,
            RoundingMode = req.RoundingMode,
            ExcludeWhenNotFiling = req.ExcludeWhenNotFiling,
            ExcludeOnEndorsements = req.ExcludeOnEndorsements,
            ExcludeOnRenewal = req.ExcludeOnRenewal,
            ExcludeOnOriginalBinder = req.ExcludeOnOriginalBinder,
            ExcludeOnMultiCarrierPolicy = req.ExcludeOnMultiCarrierPolicy,
            PayHomeState = req.PayHomeState,
            ExcludedPolicyTransactionTypes = req.ExcludedPolicyTransactionTypes,
            PayableRouting = req.PayableRouting,
            PayablePayeeId = req.PayableRouting == "Entity" ? req.PayablePayeeId : null,
            MasterPayeeWhenHomeState = req.MasterPayeeWhenHomeState,
            Notes = req.Notes,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            PremiumBrackets = req.PremiumBrackets
                .Select(b => new FeePremiumBracket { TierFrom = b.TierFrom, TierTo = b.TierTo, PercentRate = b.PercentRate })
                .ToList()
        };
        return version;
    }
}
