using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Services;

public class FeeAdminService : IFeeAdminService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

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
            .Include(v => v.PremiumBrackets)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        if (version is null) return Result<FeeRuleVersionDto>.Failure("NOT_FOUND", "Fee rule version not found");

        var nonTaxableMap = await GetNonTaxableMapAsync([version.FeeDefinitionId], ct);
        return Result<FeeRuleVersionDto>.Success(MapVersion(version, nonTaxableMap));
    }

    public async Task<Result<FeeRuleVersionDto>> CreateVersionAsync(Guid userId, CreateFeeRuleVersionRequest req, CancellationToken ct = default)
    {
        var version = BuildVersion(req, userId);
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
        var nonTaxableMap = await GetNonTaxableMapAsync([version.FeeDefinitionId], ct);
        return Result<FeeRuleVersionDto>.Success(MapVersion(version, nonTaxableMap));
    }

    public async Task<Result<FeeRuleVersionDto>> NewVersionFromExistingAsync(Guid userId, long existingVersionId, CreateFeeRuleVersionRequest req, CancellationToken ct = default)
    {
        var existing = await Db.Set<FeeRuleVersion>().FindAsync([existingVersionId], ct);
        if (existing is null) return Result<FeeRuleVersionDto>.Failure("NOT_FOUND", "Existing version not found");

        // Stamp old version's disabled_date with the new version's effective_date in one transaction
        existing.DisabledDate = req.EffectiveDate;

        var newVersion = BuildVersion(req, userId);
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
            ApplyAutomatically: v.ApplyAutomatically,
            PremiumMinThreshold: v.PremiumMinThreshold,
            PremiumMaxThreshold: v.PremiumMaxThreshold,
            PremiumThresholdBasis: v.PremiumThresholdBasis,
            RoundingMode: v.RoundingMode,
            ExcludeWhenNotFiling: v.ExcludeWhenNotFiling,
            ExcludeOnEndorsements: v.ExcludeOnEndorsements,
            PayableRouting: v.PayableRouting,
            PayablePayeeId: v.PayablePayeeId,
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

    private static FeeRuleVersion BuildVersion(CreateFeeRuleVersionRequest req, Guid userId)
    {
        var version = new FeeRuleVersion
        {
            FeeDefinitionId = req.FeeDefinitionId,
            CompanyId = req.CompanyId,
            ProducerId = req.ProducerId,
            LineOfBusiness = req.LineOfBusiness,
            StateCode = req.StateCode,
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
            ApplyAutomatically = req.ApplyAutomatically,
            PremiumMinThreshold = req.PremiumMinThreshold,
            PremiumMaxThreshold = req.PremiumMaxThreshold,
            PremiumThresholdBasis = req.PremiumThresholdBasis,
            RoundingMode = req.RoundingMode,
            ExcludeWhenNotFiling = req.ExcludeWhenNotFiling,
            ExcludeOnEndorsements = req.ExcludeOnEndorsements,
            PayableRouting = req.PayableRouting,
            PayablePayeeId = req.PayablePayeeId,
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
