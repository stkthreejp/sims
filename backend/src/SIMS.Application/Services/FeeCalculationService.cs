using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Accounting;
using InvoiceLine = SIMS.Application.DTOs.Accounting.InvoiceLine;

namespace SIMS.Application.Services;

public class FeeCalculationService : IFeeCalculationService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public FeeCalculationService(IServiceProvider sp) => _sp = sp;

    public async Task<FeeCalculationResult> CalculateAsync(PolicyContext ctx, CancellationToken ct = default)
    {
        var db = Db;

        // Step 1: Resolve all candidate rules matching scope + effective date
        var candidates = await db.Set<FeeRuleVersion>()
            .Include(v => v.FeeDefinition)
            .Include(v => v.PremiumBrackets)
            .Where(v =>
                (v.CarrierId == null || v.CarrierId == ctx.CarrierId) &&
                (v.CompanyId == null || v.CompanyId == ctx.CompanyId) &&
                (v.ProducerId == null || v.ProducerId == ctx.ProducerId) &&
                (v.LineOfBusiness == null || v.LineOfBusiness == ctx.LineOfBusiness) &&
                (v.StateCode == null || v.StateCode == ctx.StateCode) &&
                (v.City == null || v.City == ctx.City) &&
                (v.LicenseType == null || v.LicenseType == ctx.LicenseType) &&
                v.EffectiveDate <= ctx.EffectiveDate &&
                (v.DisabledDate == null || v.DisabledDate > ctx.EffectiveDate) &&
                v.SendToAccounting &&
                v.ApplyAutomatically)
            .ToListAsync(ct);

        // Per fee_definition_id, keep the most-specific (most non-null scope dims), then most-recent
        var resolved = candidates
            .GroupBy(v => v.FeeDefinitionId)
            .Select(g => g
                .OrderByDescending(v => Specificity(v))
                .ThenByDescending(v => v.EffectiveDate)
                .First())
            .ToList();

        // Step 2: Filter exclusions
        resolved = resolved
            .Where(v => !(v.ExcludeOnEndorsements && ctx.IsEndorsement))
            .Where(v => !(v.ExcludeWhenNotFiling && !ctx.IsFilingState))
            .ToList();

        // Step 3: Filter premium thresholds
        resolved = resolved
            .Where(v => v.PremiumMinThreshold == null || ctx.GrossPremium >= v.PremiumMinThreshold)
            .Where(v => v.PremiumMaxThreshold == null || ctx.GrossPremium <= v.PremiumMaxThreshold)
            .ToList();

        // Step 4: Load non-taxable state overrides for all resolved fee definitions
        var feeDefIds = resolved.Select(v => v.FeeDefinitionId).ToList();
        var nonTaxableOverrides = (await db.Set<FeeStateTaxability>()
            .Where(s => feeDefIds.Contains(s.FeeDefinitionId) && s.StateCode == ctx.StateCode && !s.IsTaxable)
            .Select(s => s.FeeDefinitionId)
            .ToListAsync(ct))
            .ToHashSet();

        // Step 5: Sort ascending by calculation_order and compute each fee
        var sortedRules = resolved
            .OrderBy(v => v.FeeDefinition.CalculationOrder)
            .ToList();

        var lines = new List<InvoiceLine>();
        decimal taxableBase = ctx.GrossPremium;

        foreach (var rule in sortedRules)
        {
            var def = rule.FeeDefinition;

            // Taxes use taxableBase (premium + all previously-calculated taxable fees).
            // All other fees use gross premium.
            var calcBase = def.FeeCategory == "Tax" ? taxableBase : ctx.GrossPremium;

            var raw = rule.CalcType switch
            {
                "Flat" => rule.FlatAmount ?? 0m,
                "Percent" => calcBase * (rule.PercentRate ?? 0m),
                "Stratified" => ComputeStratified(calcBase, rule.PremiumBrackets.ToList()),
                _ => 0m
            };

            // Apply minimum
            if (rule.MinimumAmount.HasValue && raw < rule.MinimumAmount.Value)
                raw = rule.MinimumAmount.Value;

            // Apply max $
            if (rule.MaxAmount.HasValue && raw > rule.MaxAmount.Value)
                raw = rule.MaxAmount.Value;

            // Apply max %
            if (rule.MaxPercent.HasValue)
            {
                var maxByPct = calcBase * rule.MaxPercent.Value;
                if (raw > maxByPct) raw = maxByPct;
            }

            // Multiply by locations/vehicles
            if (rule.MultiplyByLocations) raw *= ctx.LocationCount;
            if (rule.MultiplyByVehicles) raw *= ctx.VehicleCount;

            // Round
            raw = ApplyRounding(raw, rule.RoundingMode);

            // Is this fee taxable in the policy's state?
            bool effectivelyTaxable = def.IsTaxable && !nonTaxableOverrides.Contains(def.Id);

            var line = new InvoiceLine(
                FeeRuleVersionId: rule.Id,
                FeeCode: def.Code,
                FeeDisplayName: def.DisplayName,
                FeeCategory: def.FeeCategory,
                Amount: raw,
                IsTaxable: effectivelyTaxable,
                PayableRouting: rule.PayableRouting,
                PayablePayeeId: rule.PayablePayeeId,
                LedgerAccountId: def.LedgerAccountId
            );

            lines.Add(line);

            // Update taxable base for downstream taxes
            if (effectivelyTaxable)
                taxableBase += raw;
        }

        return new FeeCalculationResult(lines);
    }

    private static int Specificity(FeeRuleVersion v) =>
        (v.CompanyId != null ? 1 : 0) +
        (v.CarrierId != null ? 1 : 0) +
        (v.ProducerId != null ? 1 : 0) +
        (v.LineOfBusiness != null ? 1 : 0) +
        (v.StateCode != null ? 1 : 0) +
        (v.City != null ? 1 : 0) +
        (v.LicenseType != null ? 1 : 0);

    private static decimal ComputeStratified(decimal premium, List<FeePremiumBracket> brackets)
    {
        decimal total = 0m;
        foreach (var bracket in brackets.OrderBy(b => b.TierFrom))
        {
            if (premium <= bracket.TierFrom) break;
            var tierMax = bracket.TierTo ?? decimal.MaxValue;
            var slice = Math.Min(premium, tierMax) - bracket.TierFrom;
            total += slice * bracket.PercentRate;
        }
        return total;
    }

    private static decimal ApplyRounding(decimal amount, string mode) => mode switch
    {
        "NearestCent" => Math.Round(amount, 2, MidpointRounding.AwayFromZero),
        "RoundUp" => Math.Ceiling(amount * 100) / 100,
        "RoundDown" => Math.Floor(amount * 100) / 100,
        "NearestDollar" => Math.Round(amount, 0, MidpointRounding.AwayFromZero),
        "RoundUpDollar" => Math.Ceiling(amount),
        "RoundDownDollar" => Math.Floor(amount),
        _ => Math.Round(amount, 2, MidpointRounding.AwayFromZero)
    };
}
