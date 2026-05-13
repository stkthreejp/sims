using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Rating;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;
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
                .ThenInclude(s => s.Insured)
            .Include(q => q.Submission)
                .ThenInclude(s => s.AdditionalInterests)
            .Include(q => q.Submission)
                .ThenInclude(s => s.AdditionalInterestBlankets)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Equipment)
                    .ThenInclude(e => e.EquipmentType)
            .Include(q => q.Submission)
                .ThenInclude(s => s.Vehicles)
            .Include(q => q.Submission)
                .ThenInclude(s => s.GLCoverages)
            .Include(q => q.Submission)
                .ThenInclude(s => s.GLClassifications)
            .FirstOrDefaultAsync(q => q.Id == quoteId);

        if (quote is null)
            return Result<RatingResultDto>.Failure("NOT_FOUND", "Quote not found.");

        var assignment = await _db.CarrierRatingAssignments
            .Include(a => a.RatingPlanVersion)
                .ThenInclude(v => v.RatingPlan)
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
        var formulaKey = version.RatingPlan.FormulaKey;

        var modifier = Math.Clamp(request.ScheduleModifier, version.ScheduleMin, version.ScheduleMax);

        if (modifier != 1.0m && string.IsNullOrWhiteSpace(request.ScheduleModifierReason))
            return Result<RatingResultDto>.Failure("REASON_REQUIRED", "A reason is required when applying a schedule modifier other than 1.00.");

        // ── Dispatch to formula ──────────────────────────────────────────────
        FormulaOutput output;
        if (formulaKey == "GL_v1")
        {
            var err = TryRateGl(quote, modifier, out output);
            if (err != null) return Result<RatingResultDto>.Failure(err.Value.code, err.Value.msg);
        }
        else if (formulaKey == "APD_v1")
        {
            var err = TryRateApd(quote, version, modifier, out output);
            if (err != null) return Result<RatingResultDto>.Failure(err.Value.code, err.Value.msg);
        }
        else
        {
            var err = TryRateIm(quote, version, modifier, out output);
            if (err != null) return Result<RatingResultDto>.Failure(err.Value.code, err.Value.msg);
        }

        var inlandMarineEndorsementLines = quote.LineOfBusiness == PolicyLineOfBusiness.InlandMarine
            ? BuildInlandMarineEndorsementLines(request)
            : new List<QuoteRatingLine>();

        if (inlandMarineEndorsementLines.Count > 0)
        {
            output.Lines.AddRange(inlandMarineEndorsementLines);
            output = output with { GrandTotal = output.GrandTotal + inlandMarineEndorsementLines.Sum(l => l.LinePremium) };
        }

        var additionalInterestLines = await BuildAdditionalInterestChargeLinesAsync(quote);
        if (additionalInterestLines.Count > 0)
        {
            output.Lines.AddRange(additionalInterestLines);
            output = output with { GrandTotal = output.GrandTotal + additionalInterestLines.Sum(l => l.LinePremium) };
        }

        var endorsementPremium = inlandMarineEndorsementLines.Sum(l => l.LinePremium);
        var isInlandMarine = quote.LineOfBusiness == PolicyLineOfBusiness.InlandMarine;

        // ── Persist snapshot ─────────────────────────────────────────────────
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
            ManualPremium = output.ManualPremium,
            ScheduleModifier = modifier,
            ScheduleModifierReason = request.ScheduleModifierReason,
            GrandTotalPremium = output.GrandTotal,
            DebrisRemoval = isInlandMarine && (request.DebrisRemoval ?? true),
            RentalReimbursement = isInlandMarine && (request.RentalReimbursement ?? true),
            TowingStorageRecovery = isInlandMarine && (request.TowingStorageRecovery ?? true),
            NewlyAcquiredEquipment = isInlandMarine && (request.NewlyAcquiredEquipment ?? false),
            EndorsementPremium = endorsementPremium,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Lines = output.Lines,
        };

        _db.QuoteRatingSnapshots.Add(snapshot);

        quote.PremiumAmount = output.GrandTotal;
        quote.TotalPremium = output.GrandTotal;
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

    // ── GL_v1 ────────────────────────────────────────────────────────────────

    private static (string code, string msg)? TryRateGl(
        Domain.Entities.Quote quote, decimal modifier, out FormulaOutput output)
    {
        output = default;

        var cov = quote.Submission.GLCoverages;
        if (cov is null)
            return ("NO_GL_COVERAGES", "GL coverages have not been set up for this submission.");

        if (cov.EachOccurrence is null or 0)
            return ("MISSING_FIELD", "Each Occurrence limit is required for GL rating.");
        if (cov.MedicalExpense is null or 0)
            return ("MISSING_FIELD", "Medical Expense limit is required for GL rating.");

        var occLimit = (int)cov.EachOccurrence.Value;
        var medLimit = (int)cov.MedicalExpense.Value;

        // Default PCO limit to 1M if not specified (standard Brace program default)
        var pcoLimit = cov.ProductsCompletedOps is > 0 ? (int)cov.ProductsCompletedOps.Value : 1_000_000;

        var classifications = quote.Submission.GLClassifications
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.LocationNumber)
            .ToList();

        if (classifications.Count == 0)
            return ("NO_CLASSIFICATIONS", "No GL classifications found on this submission.");

        foreach (var c in classifications)
        {
            if (string.IsNullOrWhiteSpace(c.ClassCode))
                return ("MISSING_FIELD", $"Classification #{c.LocationNumber} has no class code.");
            if (!GlV1Formula.SupportedClassCodes.Contains(c.ClassCode))
                return ("INELIGIBLE", $"Class code {c.ClassCode} is not eligible under this rating plan.");
            if (c.Exposure is null or 0)
                return ("MISSING_FIELD", $"Classification #{c.LocationNumber} ({c.ClassCode}) has no exposure.");
        }

        var inputs = classifications
            .Select(c => new GlV1Formula.ClassInput(c.ClassCode!, c.Exposure!.Value))
            .ToList();

        GlV1Formula.RatingResult result;
        try
        {
            result = GlV1Formula.Rate(
                inputs, occLimit, pcoLimit, medLimit,
                modifier, cov.IncludeTria);
        }
        catch (InvalidOperationException ex)
        {
            return ("LOOKUP_FAIL", ex.Message);
        }

        var lines = classifications.Zip(result.Lines, (c, line) =>
        {
            var factors = new
            {
                co_rate_334 = line.CoRate334,
                co_rate_336 = line.CoRate336,
                ilf_po      = line.IlfPo,
                ilf_pco     = line.IlfPco,
                med_ilf     = line.MedIlf,
            };
            var inputs2 = new
            {
                class_code    = c.ClassCode,
                description   = c.Description,
                premium_basis = c.PremiumBasis,
                exposure      = c.Exposure,
                exposure_units = line.ExposureUnits,
                occ_limit     = occLimit,
                pco_limit     = pcoLimit,
                med_limit     = medLimit,
            };
            return new QuoteRatingLine
            {
                Id             = Guid.NewGuid(),
                ExposureRef    = $"GL-{c.LocationNumber:D3}-{c.ClassCode}",
                Inputs         = JsonSerializer.Serialize(inputs2),
                FactorsApplied = JsonSerializer.Serialize(factors),
                LinePremium    = line.LineTotal,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            };
        }).ToList();

        if (result.TriaPremium > 0)
            lines.Add(new QuoteRatingLine
            {
                Id             = Guid.NewGuid(),
                ExposureRef    = "GL-TRIA",
                Inputs         = JsonSerializer.Serialize(new { tria_rate = 0.025m, modified_premium = result.ModifiedPremium }),
                FactorsApplied = "{}",
                LinePremium    = result.TriaPremium,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow,
            });

        output = new FormulaOutput(result.ManualPremium, result.GrandTotal, lines);
        return null;
    }

    // ── IM_v1 ────────────────────────────────────────────────────────────────

    private static (string code, string msg)? TryRateIm(
        Domain.Entities.Quote quote, RatingPlanVersion version, decimal modifier, out FormulaOutput output)
    {
        output = default;

        var acceptedTypeIds = version.EligibilityRules
            .Where(r => r.Accepted)
            .Select(r => r.EquipmentTypeId)
            .ToHashSet();

        var baseRateTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "BASE_RATE");
        var deductibleTable = version.FactorTables.FirstOrDefault(ft => ft.Code == "DEDUCTIBLE_FACTOR");

        if (baseRateTable is null || deductibleTable is null)
            return ("MISSING_FACTORS", "Rating plan is missing required factor tables.");

        var equipment = quote.Submission.Equipment
            .Where(e => !e.IsDeleted)
            .OrderBy(e => e.ItemNumber)
            .ToList();

        if (equipment.Count == 0)
            return ("NO_EQUIPMENT", "No equipment items found on this submission.");

        foreach (var item in equipment)
        {
            if (item.EquipmentTypeId is null || item.EquipmentType is null)
                return ("MISSING_TYPE", $"Equipment item #{item.ItemNumber} has no equipment type assigned.");
            if (!acceptedTypeIds.Contains(item.EquipmentTypeId.Value))
                return ("INELIGIBLE", $"Equipment type '{item.EquipmentType.Name}' is not eligible under this rating plan.");
            if (item.Value is null or 0)
                return ("MISSING_VALUE", $"Equipment item #{item.ItemNumber} has no stated value.");
        }

        ImV1Formula.RatingResult result;
        try
        {
            var inputs = equipment.Select(item => new ImV1Formula.EquipmentInput(
                item.EquipmentType!.TypeNumber, item.Year, item.Value!.Value, item.Deductible)).ToList();
            result = ImV1Formula.Rate(baseRateTable, deductibleTable, inputs,
                quote.EffectiveDate.Year, modifier, version.MinimumPremium);
        }
        catch (InvalidOperationException ex)
        {
            return ("LOOKUP_FAIL", ex.Message);
        }

        var lines = equipment.Zip(result.Lines, (item, line) =>
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

        output = new FormulaOutput(result.ManualPremium, result.GrandTotal, lines);
        return null;
    }

    // ── APD_v1 ───────────────────────────────────────────────────────────────

    private static (string code, string msg)? TryRateApd(
        Domain.Entities.Quote quote, RatingPlanVersion version, decimal modifier, out FormulaOutput output)
    {
        output = default;

        var vehicles = quote.Submission.Vehicles
            .Where(v => !v.IsDeleted)
            .OrderBy(v => v.UnitNumber)
            .ToList();

        if (vehicles.Count == 0)
            return ("NO_VEHICLES", "No vehicles found on this submission.");

        // Validate required fields
        foreach (var v in vehicles)
        {
            if (v.ApdVehicleClass is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing APD vehicle class.");
            if (v.ApdRoadType is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing road type.");
            if (v.ApdAnnualMiles is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing annual miles.");
            if (v.ApdOperationCode is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing operation code.");
            if (string.IsNullOrWhiteSpace(v.ApdState))
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing state.");
            if (v.ApdStatedValue is null or 0)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing stated value.");
            if (v.ApdCompDeductible is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing comp deductible.");
            if (v.ApdCollDeductible is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing coll deductible.");
            if (v.ApdDriverAgeCode is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing driver age code.");
            if (v.ApdDriverPointsCode is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing driver points code.");
            if (v.ApdDriverExpMod is null)
                return ("MISSING_FIELD", $"Vehicle #{v.UnitNumber} is missing driver experience modifier.");

            // Enforce minimum deductibles: TT class=3 min $10k comp, Trailer class=4 min $25k comp
            var minCompDed = v.ApdVehicleClass switch { 3 => 10_000m, 4 => 25_000m, _ => 0m };
            if (v.ApdCompDeductible < minCompDed)
                return ("MIN_DEDUCTIBLE", $"Vehicle #{v.UnitNumber} comp deductible must be at least ${minCompDed:N0} for this vehicle class.");
        }

        // Look up the 8 required factor tables
        FactorTable? T(string code) => version.FactorTables.FirstOrDefault(ft => ft.Code == code);
        var compBase  = T("COMP_BASE_RATE");
        var collBase  = T("COLL_BASE_RATE");
        var mileage   = T("MILEAGE_FACTOR");
        var driver    = T("DRIVER_FACTOR");
        var operation = T("OPERATION_FACTOR");
        var state     = T("STATE_FACTOR");
        var compDed   = T("COMP_DED_FACTOR");
        var collDed   = T("COLL_DED_FACTOR");

        if (compBase is null || collBase is null || mileage is null || driver is null ||
            operation is null || state is null || compDed is null || collDed is null)
            return ("MISSING_FACTORS", "APD rating plan is missing one or more required factor tables.");

        var inputs = vehicles.Select(v => new ApdV1Formula.VehicleInput(
            UnitNumber:      v.UnitNumber,
            VehicleClass:    v.ApdVehicleClass!.Value,
            RoadType:        v.ApdRoadType!.Value,
            MileageClass:    int.Parse(ApdV1Formula.MileageClassKey(v.ApdAnnualMiles)),
            OperationCode:   v.ApdOperationCode!.Value,
            State:           v.ApdState!,
            StatedValue:     v.ApdStatedValue!.Value,
            CompDeductible:  v.ApdCompDeductible!.Value,
            CollDeductible:  v.ApdCollDeductible!.Value,
            DriverAgeCode:   v.ApdDriverAgeCode!.Value,
            DriverPointsCode: v.ApdDriverPointsCode!.Value,
            DriverExpMod:    v.ApdDriverExpMod!.Value
        )).ToList();

        ApdV1Formula.RatingResult result;
        try
        {
            result = ApdV1Formula.Rate(compBase, collBase, mileage, driver, operation, state,
                compDed, collDed, inputs, modifier, version.MinimumPremium);
        }
        catch (InvalidOperationException ex)
        {
            return ("LOOKUP_FAIL", ex.Message);
        }

        var lines = vehicles.Zip(result.Lines, (v, line) =>
        {
            var factors = new
            {
                comp_base_rate   = line.CompBaseRate,
                coll_base_rate   = line.CollBaseRate,
                mileage_factor   = line.MilageFactor,
                driver_factor    = line.DriverFactor,
                driver_exp_mod   = line.DriverExpMod,
                operation_factor = line.OperationFactor,
                state_factor     = line.StateFactor,
                comp_ded_factor  = line.CompDedFactor,
                coll_ded_factor  = line.CollDedFactor,
                value_bracket    = line.ValueBracket,
                mileage_class    = line.MileageClassKey,
            };
            var vehicleInputs = new
            {
                unit           = v.UnitNumber,
                year           = v.Year,
                make           = v.Make,
                model          = v.Model,
                stated_value   = v.ApdStatedValue,
                comp_deductible = v.ApdCompDeductible,
                coll_deductible = v.ApdCollDeductible,
                state          = v.ApdState,
                operation_code = v.ApdOperationCode,
            };
            return new QuoteRatingLine
            {
                Id = Guid.NewGuid(),
                ExposureRef = $"VEH-{v.UnitNumber:D3}",
                Inputs = JsonSerializer.Serialize(vehicleInputs),
                FactorsApplied = JsonSerializer.Serialize(factors),
                LinePremium = line.TotalPremium,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }).ToList();

        output = new FormulaOutput(result.ManualPremium, result.GrandTotal, lines);
        return null;
    }

    // ── Shared helpers ───────────────────────────────────────────────────────

    private async Task<List<QuoteRatingLine>> BuildAdditionalInterestChargeLinesAsync(Domain.Entities.Quote quote)
    {
        var interests = quote.Submission.AdditionalInterests
            .Where(i => !i.IsDeleted && i.LineOfBusiness == quote.LineOfBusiness)
            .ToList();
        var blanket = quote.Submission.AdditionalInterestBlankets
            .FirstOrDefault(b => !b.IsDeleted && b.LineOfBusiness == quote.LineOfBusiness);

        if (interests.Count == 0 && blanket == null)
            return [];

        var policyState = quote.Submission.Insured?.State?.Trim().ToUpperInvariant();
        var effectiveDate = quote.EffectiveDate;

        var candidateRules = await _db.CarrierAdditionalInterestRates
            .Where(r => r.IsActive)
            .Where(r => r.CarrierId == null || r.CarrierId == quote.CarrierId)
            .Where(r => r.LineOfBusiness == null || r.LineOfBusiness == quote.LineOfBusiness)
            .Where(r => r.State == null || (policyState != null && r.State == policyState))
            .Where(r => r.EffectiveDate == null || r.EffectiveDate <= effectiveDate)
            .Where(r => r.ExpirationDate == null || r.ExpirationDate > effectiveDate)
            .ToListAsync();

        if (candidateRules.Count == 0)
            return [];

        var lines = new List<QuoteRatingLine>();

        foreach (var coverageType in Enum.GetValues<AdditionalInterestCoverageType>())
        {
            var matchingInterests = interests
                .Where(i => RequestsCoverage(i, coverageType))
                .ToList();
            var blanketRequested = RequestsBlanketCoverage(blanket, coverageType);

            if (matchingInterests.Count == 0 && !blanketRequested)
                continue;

            var rule = candidateRules
                .Where(r => r.CoverageType == coverageType)
                .OrderByDescending(RuleSpecificity)
                .ThenByDescending(r => blanketRequested && r.ChargeMethod != AdditionalInterestChargeMethod.PerInterest)
                .ThenByDescending(r => r.EffectiveDate ?? DateOnly.MinValue)
                .FirstOrDefault();

            if (rule is null)
                continue;

            var chargeCount = blanketRequested ? 1 : matchingInterests.Count;
            var amount = CalculateAdditionalInterestCharge(rule, chargeCount);
            var coverageLabel = AdditionalInterestLabel(coverageType);

            lines.Add(new QuoteRatingLine
            {
                Id = Guid.NewGuid(),
                ExposureRef = $"ADDINT-{AdditionalInterestCode(coverageType)}",
                Inputs = JsonSerializer.Serialize(new
                {
                    type = coverageLabel,
                    blanket = blanketRequested,
                    count = chargeCount,
                    named_count = matchingInterests.Count,
                    names = matchingInterests.Select(i => i.Name).ToList(),
                    applies_to = matchingInterests
                        .Select(i => i.AppliesToType.ToString())
                        .Distinct()
                        .ToList(),
                }),
                FactorsApplied = JsonSerializer.Serialize(new
                {
                    rule_id = rule.Id,
                    method = rule.ChargeMethod.ToString(),
                    carrier_scope = rule.CarrierId?.ToString() ?? "All",
                    lob_scope = rule.LineOfBusiness?.ToString() ?? "All",
                    state_scope = rule.State ?? "All",
                    per_interest_amount = rule.PerInterestAmount,
                    blanket_amount = rule.BlanketAmount,
                    minimum = rule.MinimumCharge,
                    maximum = rule.MaximumCharge,
                }),
                LinePremium = amount,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        return lines;
    }

    private static decimal CalculateAdditionalInterestCharge(CarrierAdditionalInterestRate rule, int interestCount)
        => AdditionalInterestChargeCalculator.Calculate(
            rule.ChargeMethod,
            interestCount,
            rule.PerInterestAmount,
            rule.BlanketAmount,
            rule.MinimumCharge,
            rule.MaximumCharge);

    private static bool RequestsCoverage(SubmissionAdditionalInterest interest, AdditionalInterestCoverageType coverageType) =>
        coverageType switch
        {
            AdditionalInterestCoverageType.AdditionalInsured => interest.AdditionalInsured,
            AdditionalInterestCoverageType.LossPayee => interest.LossPayee,
            AdditionalInterestCoverageType.WaiverOfSubrogation => interest.WaiverOfSubrogation,
            AdditionalInterestCoverageType.PrimaryNonContributory => interest.PrimaryNonContributory,
            _ => false
        };

    private static bool RequestsBlanketCoverage(SubmissionAdditionalInterestBlanket? blanket, AdditionalInterestCoverageType coverageType) =>
        blanket != null && coverageType switch
        {
            AdditionalInterestCoverageType.AdditionalInsured => blanket.AdditionalInsured,
            AdditionalInterestCoverageType.WaiverOfSubrogation => blanket.WaiverOfSubrogation,
            AdditionalInterestCoverageType.PrimaryNonContributory => blanket.PrimaryNonContributory,
            _ => false
        };

    private static int RuleSpecificity(CarrierAdditionalInterestRate rule) =>
        (rule.CarrierId.HasValue ? 1 : 0) +
        (rule.LineOfBusiness.HasValue ? 1 : 0) +
        (!string.IsNullOrWhiteSpace(rule.State) ? 1 : 0);

    private static string AdditionalInterestCode(AdditionalInterestCoverageType coverageType) =>
        coverageType switch
        {
            AdditionalInterestCoverageType.AdditionalInsured => "AI",
            AdditionalInterestCoverageType.LossPayee => "LP",
            AdditionalInterestCoverageType.WaiverOfSubrogation => "WOS",
            AdditionalInterestCoverageType.PrimaryNonContributory => "PNC",
            _ => coverageType.ToString().ToUpperInvariant()
        };

    private static string AdditionalInterestLabel(AdditionalInterestCoverageType coverageType) =>
        coverageType switch
        {
            AdditionalInterestCoverageType.AdditionalInsured => "Additional Insured",
            AdditionalInterestCoverageType.LossPayee => "Loss Payee",
            AdditionalInterestCoverageType.WaiverOfSubrogation => "Waiver of Subrogation",
            AdditionalInterestCoverageType.PrimaryNonContributory => "Primary & Non-Contributory",
            _ => coverageType.ToString()
        };

    private record struct FormulaOutput(decimal ManualPremium, decimal GrandTotal, List<QuoteRatingLine> Lines);

    private static List<QuoteRatingLine> BuildInlandMarineEndorsementLines(RateQuoteRequest request)
    {
        var selections = new[]
        {
            new { Selected = request.DebrisRemoval ?? true, Code = "DEBRIS", Name = "Debris Removal", Premium = 250m },
            new { Selected = request.RentalReimbursement ?? true, Code = "RENTAL", Name = "Rental Reimbursement", Premium = 500m },
            new { Selected = request.TowingStorageRecovery ?? true, Code = "TOWING", Name = "Towing, Storage & Recovery", Premium = 175m },
            new { Selected = request.NewlyAcquiredEquipment ?? false, Code = "NEWLY", Name = "Newly Acquired Equipment", Premium = 0m },
        };

        return selections
            .Where(e => e.Selected && e.Premium > 0)
            .Select(e => new QuoteRatingLine
            {
                Id = Guid.NewGuid(),
                ExposureRef = $"IM-END-{e.Code}",
                Inputs = JsonSerializer.Serialize(new { type = e.Name }),
                FactorsApplied = JsonSerializer.Serialize(new { basis = "flat" }),
                LinePremium = e.Premium,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            })
            .ToList();
    }

    private static RatingResultDto MapSnapshotToDto(QuoteRatingSnapshot s, RatingPlanVersion v, string? ratedByName) => new()
    {
        SnapshotId = s.Id,
        ManualPremium = s.ManualPremium,
        ScheduleModifier = s.ScheduleModifier,
        ScheduleModifierReason = s.ScheduleModifierReason,
        DebrisRemoval = s.DebrisRemoval,
        RentalReimbursement = s.RentalReimbursement,
        TowingStorageRecovery = s.TowingStorageRecovery,
        NewlyAcquiredEquipment = s.NewlyAcquiredEquipment,
        EndorsementPremium = s.EndorsementPremium,
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
