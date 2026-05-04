using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Rating;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class RatingEngineService : IRatingEngineService
{
    private readonly ApplicationDbContext _db;

    public RatingEngineService(ApplicationDbContext db) => _db = db;

    public async Task<Result<RatingResultDto>> RateAsync(Guid quoteId, RateQuoteRequest request, Guid ratedById)
    {
        var quote = await _db.Quotes
            .Include(q => q.Submission)
                .ThenInclude(s => s.Equipment)
                    .ThenInclude(e => e.EquipmentType)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote is null)
            return Result<RatingResultDto>.Failure("NOT_FOUND", "Quote not found.");

        var assignment = await _db.CarrierRatingAssignments
            .Include(a => a.RatingPlanVersion)
                .ThenInclude(v => v.FactorTables)
                    .ThenInclude(ft => ft.Rows)
            .Include(a => a.RatingPlanVersion)
                .ThenInclude(v => v.EligibilityRules)
                    .ThenInclude(er => er.EquipmentType)
            .FirstOrDefaultAsync(a => a.CarrierId == quote.CarrierId && a.LineOfBusiness == quote.LineOfBusiness);

        if (assignment is null)
            return Result<RatingResultDto>.Failure("NO_RATING_PLAN", "No rating plan assigned for this carrier and line of business.");

        var version = assignment.RatingPlanVersion;

        var modifier = Math.Clamp(request.ScheduleModifier, version.ScheduleMin, version.ScheduleMax);

        // A schedule modifier other than 1.00 must be justified.
        if (modifier != 1.0m && string.IsNullOrWhiteSpace(request.ScheduleModifierReason))
            return Result<RatingResultDto>.Failure("REASON_REQUIRED", "A reason is required when applying a schedule modifier other than 1.00.");

        var acceptedTypeIds = version.EligibilityRules
            .Where(r => r.Accepted)
            .Select(r => r.EquipmentTypeId)
            .ToHashSet();

        var baseRateTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "BASE_RATE");
        var deductibleTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "DEDUCTIBLE_FACTOR");

        if (baseRateTable is null || deductibleTable is null)
            return Result<RatingResultDto>.Failure("MISSING_FACTORS", "Rating plan is missing required factor tables.");

        var equipment = quote.Submission.Equipment
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.ItemNumber)
            .ToList();

        if (equipment.Count == 0)
            return Result<RatingResultDto>.Failure("NO_EQUIPMENT", "No equipment items found on this submission.");

        // Validate items before rating
        foreach (var item in equipment)
        {
            if (item.EquipmentTypeId is null || item.EquipmentType is null)
                return Result<RatingResultDto>.Failure("MISSING_TYPE", $"Equipment item #{item.ItemNumber} has no equipment type assigned.");
            if (!acceptedTypeIds.Contains(item.EquipmentTypeId.Value))
                return Result<RatingResultDto>.Failure("INELIGIBLE", $"Equipment type '{item.EquipmentType.Name}' is not eligible under this rating plan.");
            if (item.Value is null or 0)
                return Result<RatingResultDto>.Failure("MISSING_VALUE", $"Equipment item #{item.ItemNumber} has no stated value.");
        }

        // Delegate pure math to ImV1Formula
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
            return Result<RatingResultDto>.Failure("LOOKUP_FAIL", ex.Message);
        }

        var manualPremium = ratingResult.ManualPremium;
        var grandTotal = ratingResult.GrandTotal;

        var lines = equipment.Zip(ratingResult.Lines, (item, line) =>
        {
            var factors = new { base_rate = line.BaseRate, deductible_factor = line.DeductibleFactor, age_band = line.AgeBand, deductible = line.DeductibleKey };
            var inputs = new { type = item.EquipmentType!.Name, year = item.Year, value = item.Value, deductible = line.DeductibleKey };
            return new QuoteRatingLine
            {
                Id = Guid.NewGuid(),
                ExposureRef = $"EQ-{item.ItemNumber:D3}",
                Inputs = JsonSerializer.Serialize(inputs),
                FactorsApplied = JsonSerializer.Serialize(factors),
                LinePremium = line.LinePremium,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }).ToList();

        // Replace existing non-bound snapshots for this quote
        var existing = await _db.QuoteRatingSnapshots
            .Where(s => s.QuoteId == quoteId && !s.IsBoundSnapshot)
            .ToListAsync();
        _db.QuoteRatingSnapshots.RemoveRange(existing);

        var snapshot = new QuoteRatingSnapshot
        {
            Id = Guid.NewGuid(),
            QuoteId = quoteId,
            RatingPlanVersionId = version.Id,
            RatedAt = DateTime.UtcNow,
            RatedById = ratedById,
            ManualPremium = manualPremium,
            ScheduleModifier = modifier,
            ScheduleModifierReason = request.ScheduleModifierReason,
            GrandTotalPremium = grandTotal,
            EndorsementPremium = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = lines,
        };

        _db.QuoteRatingSnapshots.Add(snapshot);

        // Stamp the quote's premium fields so existing bind flow picks it up
        quote.PremiumAmount = grandTotal;
        quote.TotalPremium = grandTotal;
        quote.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        var ratedByName = await _db.Users
            .Where(u => u.Id == ratedById)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();

        return Result<RatingResultDto>.Success(MapSnapshotToDto(snapshot, version, ratedByName));
    }

    public async Task<Result<RatingResultDto>> GetLatestSnapshotAsync(Guid quoteId)
    {
        var snapshot = await _db.QuoteRatingSnapshots
            .Include(s => s.Lines)
            .Include(s => s.RatingPlanVersion)
            .Where(s => s.QuoteId == quoteId)
            .OrderByDescending(s => s.RatedAt)
            .FirstOrDefaultAsync();

        if (snapshot is null)
            return Result<RatingResultDto>.Failure("NOT_FOUND", "No rating snapshot for this quote.");

        var ratedByName = await _db.Users
            .Where(u => u.Id == snapshot.RatedById)
            .Select(u => u.FirstName + " " + u.LastName)
            .FirstOrDefaultAsync();

        return Result<RatingResultDto>.Success(MapSnapshotToDto(snapshot, snapshot.RatingPlanVersion, ratedByName));
    }

    private static RatingResultDto MapSnapshotToDto(QuoteRatingSnapshot s, RatingPlanVersion v, string? ratedByName) => new()
    {
        SnapshotId = s.Id,
        ManualPremium = s.ManualPremium,
        ScheduleModifier = s.ScheduleModifier,
        ScheduleModifierReason = s.ScheduleModifierReason,
        GrandTotalPremium = s.GrandTotalPremium,
        RatedAt = s.RatedAt,
        RatedById = s.RatedById,
        RatedByName = ratedByName,
        IsBoundSnapshot = s.IsBoundSnapshot,
        ScheduleMin = v.ScheduleMin,
        ScheduleMax = v.ScheduleMax,
        MinimumPremium = v.MinimumPremium,
        Lines = s.Lines
            .OrderBy(l => l.ExposureRef)
            .Select(l => new RatingLineDto
            {
                ExposureRef = l.ExposureRef,
                LinePremium = l.LinePremium,
                Inputs = l.Inputs,
                FactorsApplied = l.FactorsApplied,
            }).ToList(),
    };

}
