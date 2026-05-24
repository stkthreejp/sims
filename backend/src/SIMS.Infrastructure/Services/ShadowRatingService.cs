using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.DTOs.Rating;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Rating;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class ShadowRatingService : IShadowRatingService
{
    private readonly ApplicationDbContext _db;
    private readonly ICarrierRatingAssignmentService _carrierRatingAssignments;

    public ShadowRatingService(ApplicationDbContext db, ICarrierRatingAssignmentService carrierRatingAssignments)
    {
        _db = db;
        _carrierRatingAssignments = carrierRatingAssignments;
    }

    public async Task<Result<ShadowRatingResultDto>> ShadowRateAsync(Guid quoteId, RateQuoteRequest request, Guid ratedById)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission)
                .ThenInclude(s => s.Equipment)
                    .ThenInclude(e => e.EquipmentType)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote is null)
            return Result<ShadowRatingResultDto>.Failure("NOT_FOUND", "Quote not found.");

        var assignment = await _carrierRatingAssignments.GetActiveAssignmentAsync(quote.CarrierId, quote.LineOfBusiness, quote.ProgramId);

        if (assignment is null)
            return Result<ShadowRatingResultDto>.Failure("NO_RATING_PLAN", "No rating plan assigned for this carrier and line of business.");

        var version = await _db.RatingPlanVersions
            .Include(v => v.FactorTables)
                .ThenInclude(ft => ft.Rows)
            .Include(v => v.EligibilityRules)
                .ThenInclude(er => er.EquipmentType)
            .FirstOrDefaultAsync(v => v.Id == assignment.RatingPlanVersionId);
        if (version is null)
            return Result<ShadowRatingResultDto>.Failure("NO_RATING_PLAN", "No rating plan assigned for this carrier and line of business.");

        var modifier = Math.Clamp(request.ScheduleModifier, version.ScheduleMin, version.ScheduleMax);

        if (modifier != 1.0m && string.IsNullOrWhiteSpace(request.ScheduleModifierReason))
            return Result<ShadowRatingResultDto>.Failure("REASON_REQUIRED", "A reason is required when applying a schedule modifier other than 1.00.");

        var acceptedTypeIds = version.EligibilityRules
            .Where(r => r.Accepted)
            .Select(r => r.EquipmentTypeId)
            .ToHashSet();

        var baseRateTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "BASE_RATE");
        var deductibleTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "DEDUCTIBLE_FACTOR");

        if (baseRateTable is null || deductibleTable is null)
            return Result<ShadowRatingResultDto>.Failure("MISSING_FACTORS", "Rating plan is missing required factor tables.");

        var equipment = quote.Submission.Equipment
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.ItemNumber)
            .ToList();

        if (equipment.Count == 0)
            return Result<ShadowRatingResultDto>.Failure("NO_EQUIPMENT", "No equipment items found on this submission.");

        foreach (var item in equipment)
        {
            if (item.EquipmentTypeId is null || item.EquipmentType is null)
                return Result<ShadowRatingResultDto>.Failure("MISSING_TYPE", $"Equipment item #{item.ItemNumber} has no equipment type assigned.");
            if (!acceptedTypeIds.Contains(item.EquipmentTypeId.Value))
                return Result<ShadowRatingResultDto>.Failure("INELIGIBLE", $"Equipment type '{item.EquipmentType.Name}' is not eligible under this rating plan.");
            if (item.Value is null or 0)
                return Result<ShadowRatingResultDto>.Failure("MISSING_VALUE", $"Equipment item #{item.ItemNumber} has no stated value.");
        }

        var formulaInputs = equipment.Select(item => new ImV1Formula.EquipmentInput(
            item.EquipmentType!.TypeNumber, item.Year, item.Value!.Value, item.Deductible)).ToList();

        ImV1Formula.RatingResult ratingResult;
        try
        {
            ratingResult = ImV1Formula.Rate(baseRateTable, deductibleTable, formulaInputs,
                quote.EffectiveDate.Year, modifier, version.MinimumPremium);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ShadowRatingResultDto>.Failure("LOOKUP_FAIL", ex.Message);
        }

        var shadowPremium = ratingResult.GrandTotal;
        var actualPremium = quote.PremiumAmount;
        var deltaAmount = shadowPremium - actualPremium;
        var deltaPct = actualPremium != 0 ? deltaAmount / actualPremium * 100m : 0m;

        var snapshotLines = equipment.Zip(ratingResult.Lines, (item, line) => new
        {
            exposureRef = $"EQ-{item.ItemNumber:D3}",
            linePremium = line.LinePremium,
            baseRate = line.BaseRate,
            deductibleFactor = line.DeductibleFactor,
        });

        var result = new ShadowRatingResult
        {
            QuoteId = quoteId,
            RatingPlanVersionId = version.Id,
            RatedAt = DateTime.UtcNow,
            RatedById = ratedById,
            ShadowPremium = shadowPremium,
            ActualPremium = actualPremium,
            DeltaAmount = deltaAmount,
            DeltaPct = deltaPct,
            ScheduleModifier = modifier,
            SnapshotJson = JsonSerializer.Serialize(snapshotLines),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.ShadowRatingResults.Add(result);
        await _db.SaveChangesAsync();

        var ratedByName = await _db.Users
            .Where(u => u.Id == ratedById)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();

        var insuredName = await _db.Submissions
            .Where(s => s.Id == quote.SubmissionId)
            .Select(s => s.Insured.CompanyName ?? (s.Insured.FirstName + " " + s.Insured.LastName).Trim())
            .FirstOrDefaultAsync();

        var planName = await _db.RatingPlans
            .Where(p => p.Versions.Any(v => v.Id == version.Id))
            .Select(p => p.Name)
            .FirstOrDefaultAsync();

        return Result<ShadowRatingResultDto>.Success(new ShadowRatingResultDto
        {
            Id = result.Id,
            QuoteId = quoteId,
            QuoteNumber = quote.QuoteNumber,
            InsuredName = insuredName ?? "",
            RatingPlanVersionId = version.Id,
            PlanName = planName ?? "",
            VersionNumber = version.VersionNumber,
            RatedAt = result.RatedAt,
            RatedById = ratedById,
            RatedByName = ratedByName ?? "",
            ShadowPremium = shadowPremium,
            ActualPremium = actualPremium,
            DeltaAmount = deltaAmount,
            DeltaPct = deltaPct,
            IsOutlier = Math.Abs(deltaPct) > 0.5m,
            ScheduleModifier = modifier,
        });
    }

    public async Task<ShadowSettingsDto> GetShadowSettingsAsync(CancellationToken ct = default)
    {
        var s = await _db.RatingSettings.FirstOrDefaultAsync(ct);
        return s is null ? new ShadowSettingsDto() : new ShadowSettingsDto
        {
            GL = s.ShadowModeGL,
            IM = s.ShadowModeIM,
            AL = s.ShadowModeAL,
            APD = s.ShadowModeAPD,
        };
    }

    public async Task<bool> IsShadowModeEnabledForLobAsync(PolicyLineOfBusiness lob, CancellationToken ct = default)
    {
        var settings = await GetShadowSettingsAsync(ct);
        return settings.IsEnabledFor(lob);
    }

    public async Task SetShadowModeForLobAsync(PolicyLineOfBusiness lob, bool enabled, CancellationToken ct = default)
    {
        var settings = await _db.RatingSettings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new RatingSettings { Id = new Guid("00000000-0000-0000-0000-000000000001"), CreatedAt = DateTime.UtcNow };
            _db.RatingSettings.Add(settings);
        }
        switch (lob)
        {
            case PolicyLineOfBusiness.GeneralLiability:   settings.ShadowModeGL  = enabled; break;
            case PolicyLineOfBusiness.InlandMarine:        settings.ShadowModeIM  = enabled; break;
            case PolicyLineOfBusiness.AutoLiability:       settings.ShadowModeAL  = enabled; break;
            case PolicyLineOfBusiness.AutoPhysicalDamage:  settings.ShadowModeAPD = enabled; break;
        }
        settings.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ShadowRatingResultDto>> GetResultsAsync(int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);

        var results = await _db.ShadowRatingResults
            .Where(r => !r.IsDeleted && r.RatedAt >= since)
            .OrderByDescending(r => r.RatedAt)
            .Select(r => new
            {
                r.Id,
                r.QuoteId,
                QuoteNumber = r.Quote.QuoteNumber,
                InsuredName = r.Quote.Submission.Insured.CompanyName ?? (r.Quote.Submission.Insured.FirstName + " " + r.Quote.Submission.Insured.LastName).Trim(),
                r.RatingPlanVersionId,
                PlanName = r.RatingPlanVersion.RatingPlan.Name,
                r.RatingPlanVersion.VersionNumber,
                r.RatedAt,
                r.RatedById,
                RatedByName = r.RatedBy.FirstName + " " + r.RatedBy.LastName,
                r.ShadowPremium,
                r.ActualPremium,
                r.DeltaAmount,
                r.DeltaPct,
                r.ScheduleModifier,
            })
            .ToListAsync(ct);

        return results.Select(r => new ShadowRatingResultDto
        {
            Id = r.Id,
            QuoteId = r.QuoteId,
            QuoteNumber = r.QuoteNumber,
            InsuredName = r.InsuredName,
            RatingPlanVersionId = r.RatingPlanVersionId,
            PlanName = r.PlanName,
            VersionNumber = r.VersionNumber,
            RatedAt = r.RatedAt,
            RatedById = r.RatedById,
            RatedByName = r.RatedByName,
            ShadowPremium = r.ShadowPremium,
            ActualPremium = r.ActualPremium,
            DeltaAmount = r.DeltaAmount,
            DeltaPct = r.DeltaPct,
            IsOutlier = Math.Abs(r.DeltaPct) > 0.5m,
            ScheduleModifier = r.ScheduleModifier,
        }).ToList();
    }
}
