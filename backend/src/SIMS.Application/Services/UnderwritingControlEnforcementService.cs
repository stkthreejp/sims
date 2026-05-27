using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.DTOs.UWWriteup;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Services;

public class UnderwritingControlEnforcementService : IUnderwritingControlEnforcementService
{
    private static readonly UnderwritingControlStage[] BindLikeStages =
    [
        UnderwritingControlStage.Submission,
        UnderwritingControlStage.Quote,
        UnderwritingControlStage.Bind
    ];

    private readonly DbContext _db;

    public UnderwritingControlEnforcementService(DbContext db) => _db = db;

    public async Task<UnderwritingControlEvaluationSummaryDto> EvaluateQuoteAsync(Guid quoteId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default)
    {
        var quote = await _db.Set<Quote>()
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .Include(q => q.Submission).ThenInclude(s => s.Equipment)
            .Include(q => q.Submission).ThenInclude(s => s.Vehicles)
            .Include(q => q.Submission).ThenInclude(s => s.Drivers)
            .Include(q => q.Submission).ThenInclude(s => s.GLCoverages)
            .Include(q => q.Submission).ThenInclude(s => s.GLClassifications)
            .Include(q => q.Submission).ThenInclude(s => s.LossYears).ThenInclude(y => y.Claims)
            .Include(q => q.UWWriteup)
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == quoteId && !q.IsDeleted, ct);

        if (quote is null)
            return new UnderwritingControlEvaluationSummaryDto([]);

        var controls = await GetMatchingControlsAsync(
            quote.ProgramId,
            quote.LineOfBusiness,
            quote.CarrierId,
            quote.Submission.Insured.State,
            StagesFor(stage),
            ct);

        var scheduleModifier = await LatestScheduleModifierAsync(quote.Id, ct);
        var context = BuildQuoteContext(quote, scheduleModifier);
        var results = new List<UnderwritingControlEnforcementResult>();
        foreach (var control in controls)
            results.Add(await UpsertResultAsync(control, UnderwritingControlTargetType.Quote, quoteId, stage, context, ct));

        await _db.SaveChangesAsync(ct);
        return new UnderwritingControlEvaluationSummaryDto(results.Select(Map).ToList());
    }

    public async Task<UnderwritingControlEvaluationSummaryDto> EvaluatePolicyAsync(Guid policyId, UnderwritingControlStage stage, Guid evaluatedByUserId, CancellationToken ct = default)
    {
        var policy = await _db.Set<Policy>()
            .Include(p => p.Submission).ThenInclude(s => s.Insured)
            .Include(p => p.Submission).ThenInclude(s => s.Equipment)
            .Include(p => p.Submission).ThenInclude(s => s.Vehicles)
            .Include(p => p.Submission).ThenInclude(s => s.Drivers)
            .Include(p => p.Submission).ThenInclude(s => s.GLCoverages)
            .Include(p => p.Submission).ThenInclude(s => s.GLClassifications)
            .Include(p => p.Submission).ThenInclude(s => s.LossYears).ThenInclude(y => y.Claims)
            .Include(p => p.BoundQuote).ThenInclude(q => q!.UWWriteup)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == policyId && !p.IsDeleted, ct);

        if (policy is null)
            return new UnderwritingControlEvaluationSummaryDto([]);

        var controls = await GetMatchingControlsAsync(
            policy.ProgramId,
            policy.LineOfBusiness,
            policy.CarrierId,
            policy.Submission.Insured.State,
            StagesFor(stage),
            ct);

        var scheduleModifier = policy.BoundQuote is null
            ? null
            : await LatestScheduleModifierAsync(policy.BoundQuote.Id, ct);
        var context = BuildPolicyContext(policy, scheduleModifier);
        var results = new List<UnderwritingControlEnforcementResult>();
        foreach (var control in controls)
            results.Add(await UpsertResultAsync(control, UnderwritingControlTargetType.Policy, policyId, stage, context, ct));

        await _db.SaveChangesAsync(ct);
        return new UnderwritingControlEvaluationSummaryDto(results.Select(Map).ToList());
    }

    public async Task<IReadOnlyList<UnderwritingControlEnforcementResultDto>> GetForTargetAsync(UnderwritingControlTargetType targetType, Guid targetId, CancellationToken ct = default)
    {
        var results = await _db.Set<UnderwritingControlEnforcementResult>()
            .Include(r => r.GuidelineControl)
            .Where(r => r.TargetType == targetType && r.TargetId == targetId)
            .OrderByDescending(r => r.EvaluatedAt)
            .ToListAsync(ct);

        return results.Select(Map).ToList();
    }

    public async Task<Result<UnderwritingControlEnforcementResultDto>> OverrideAsync(Guid resultId, Guid userId, string reason, CancellationToken ct = default)
    {
        var result = await _db.Set<UnderwritingControlEnforcementResult>()
            .Include(r => r.GuidelineControl)
            .FirstOrDefaultAsync(r => r.Id == resultId, ct);

        if (result is null)
            return Result<UnderwritingControlEnforcementResultDto>.Failure("RESULT_NOT_FOUND", "Enforcement result was not found.");
        if (!result.OverrideAllowed)
            return Result<UnderwritingControlEnforcementResultDto>.Failure("OVERRIDE_NOT_ALLOWED", "This control result cannot be overridden.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result<UnderwritingControlEnforcementResultDto>.Failure("OVERRIDE_REASON_REQUIRED", "An override reason is required.");

        result.Status = UnderwritingControlEvaluationStatus.Overridden;
        result.OverriddenByUserId = userId;
        result.OverriddenAt = DateTime.UtcNow;
        result.OverrideReason = reason.Trim();
        result.Message = $"{result.GuidelineControl.Label} was overridden.";
        await _db.SaveChangesAsync(ct);

        return Result<UnderwritingControlEnforcementResultDto>.Success(Map(result));
    }

    private async Task<List<UnderwritingGuidelineControl>> GetMatchingControlsAsync(
        Guid? programId,
        PolicyLineOfBusiness lineOfBusiness,
        Guid carrierId,
        string? stateCode,
        IReadOnlyList<UnderwritingControlStage> stages,
        CancellationToken ct)
    {
        var state = NormalizeState(stateCode);
        return await _db.Set<UnderwritingGuidelineControl>()
            .Where(c => c.Status == UnderwritingControlStatus.Published
                && stages.Contains(c.Stage)
                && ((c.ProgramId.HasValue
                        && programId.HasValue
                        && c.ProgramId.Value == programId.Value
                        && c.LineOfBusiness == lineOfBusiness
                        && (c.CarrierId == null || c.CarrierId == carrierId)
                        && (c.StateCode == "ALL" || c.StateCode == state))
                    || (c.ProgramId == null
                        && c.LineOfBusiness == lineOfBusiness
                        && (c.CarrierId == null || c.CarrierId == carrierId)
                        && (c.StateCode == "ALL" || c.StateCode == state))))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Label)
            .ToListAsync(ct);
    }

    private async Task<UnderwritingControlEnforcementResult> UpsertResultAsync(
        UnderwritingGuidelineControl control,
        UnderwritingControlTargetType targetType,
        Guid targetId,
        UnderwritingControlStage evaluationStage,
        ControlEvaluationContext context,
        CancellationToken ct)
    {
        var evaluation = Evaluate(control, context);
        var result = await _db.Set<UnderwritingControlEnforcementResult>()
            .Include(r => r.GuidelineControl)
            .SingleOrDefaultAsync(r => r.GuidelineControlId == control.Id
                && r.TargetType == targetType
                && r.TargetId == targetId
                && r.Stage == evaluationStage, ct);

        if (result is null)
        {
            result = new UnderwritingControlEnforcementResult
            {
                GuidelineControlId = control.Id,
                GuidelineControl = control,
                TargetType = targetType,
                TargetId = targetId,
                Stage = evaluationStage
            };
            _db.Set<UnderwritingControlEnforcementResult>().Add(result);
        }
        else if (result.Status == UnderwritingControlEvaluationStatus.Overridden && evaluation.Status == UnderwritingControlEvaluationStatus.Blocked)
        {
            result.GuidelineControl = control;
            result.EvaluatedAt = DateTime.UtcNow;
            return result;
        }

        result.GuidelineControl = control;
        result.Status = evaluation.Status;
        result.IsBlocking = control.IsBlocking;
        result.OverrideAllowed = control.OverrideAllowed;
        result.OverridePermission = control.OverridePermission;
        result.Message = evaluation.Message;
        result.ConditionJson = control.ConditionJson;
        result.InputSnapshotJson = JsonSerializer.Serialize(context.Snapshot);
        result.EvaluatedAt = DateTime.UtcNow;
        if (evaluation.Status != UnderwritingControlEvaluationStatus.Blocked)
        {
            result.OverriddenByUserId = null;
            result.OverriddenAt = null;
            result.OverrideReason = null;
        }

        return result;
    }

    private static ControlEvaluation Evaluate(UnderwritingGuidelineControl control, ControlEvaluationContext context)
    {
        if (string.IsNullOrWhiteSpace(control.ConditionJson))
            return new ControlEvaluation(StatusForAppliedControl(control), $"{control.Label} applies.");

        ConditionRule? rule;
        try
        {
            rule = JsonSerializer.Deserialize<ConditionRule>(control.ConditionJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return new ControlEvaluation(UnderwritingControlEvaluationStatus.UnknownField, $"{control.Label} has invalid condition JSON.");
        }

        if (rule is null || string.IsNullOrWhiteSpace(rule.Field) || string.IsNullOrWhiteSpace(rule.Operator))
            return new ControlEvaluation(UnderwritingControlEvaluationStatus.UnknownField, $"{control.Label} has an incomplete condition.");

        if (context.Values.TryGetValue(rule.Field, out var actual))
        {
            var applies = Compare(actual, rule.Operator, rule.Value);
            return applies
                ? new ControlEvaluation(StatusForAppliedControl(control), $"{control.Label} applies.")
                : new ControlEvaluation(UnderwritingControlEvaluationStatus.NotApplicable, $"{control.Label} does not apply.");
        }

        if (context.TextValues.TryGetValue(rule.Field, out var textValues))
        {
            var applies = CompareTextList(textValues, rule.Operator, rule.Value);
            return applies
                ? new ControlEvaluation(StatusForAppliedControl(control), $"{control.Label} applies.")
                : new ControlEvaluation(UnderwritingControlEvaluationStatus.NotApplicable, $"{control.Label} does not apply.");
        }

        return new ControlEvaluation(UnderwritingControlEvaluationStatus.UnknownField, $"{control.Label} references unsupported field '{rule.Field}'.");
    }

    private static UnderwritingControlEvaluationStatus StatusForAppliedControl(UnderwritingGuidelineControl control)
    {
        if (control.IsBlocking || control.Severity == UnderwritingControlSeverity.HardBlock)
            return UnderwritingControlEvaluationStatus.Blocked;
        if (control.Severity == UnderwritingControlSeverity.ReferralRequired)
            return UnderwritingControlEvaluationStatus.ReferralRequired;
        if (control.Severity == UnderwritingControlSeverity.Warning)
            return UnderwritingControlEvaluationStatus.Warning;
        return UnderwritingControlEvaluationStatus.Passed;
    }

    private static bool Compare(decimal actual, string op, JsonElement expected)
    {
        if (!TryGetDecimal(expected, out var expectedDecimal))
            return false;

        return op.Trim() switch
        {
            ">" or "greaterThan" => actual > expectedDecimal,
            ">=" or "greaterThanOrEqual" => actual >= expectedDecimal,
            "<" or "lessThan" => actual < expectedDecimal,
            "<=" or "lessThanOrEqual" => actual <= expectedDecimal,
            "==" or "=" or "equals" => actual == expectedDecimal,
            "!=" or "notEquals" => actual != expectedDecimal,
            _ => false
        };
    }

    private static bool CompareTextList(IReadOnlyCollection<string> actual, string op, JsonElement expected)
    {
        var expectedText = expected.ValueKind == JsonValueKind.String
            ? expected.GetString()
            : expected.ToString();
        if (string.IsNullOrWhiteSpace(expectedText))
            return false;

        return op.Trim() switch
        {
            "contains" => actual.Contains(expectedText, StringComparer.OrdinalIgnoreCase),
            "notContains" => !actual.Contains(expectedText, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryGetDecimal(JsonElement element, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Number)
            return element.TryGetDecimal(out value);
        if (element.ValueKind == JsonValueKind.String)
            return decimal.TryParse(element.GetString(), out value);

        value = 0;
        return false;
    }

    private static IMWriteupPayload? ParseWriteupPayload(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<IMWriteupPayload>(payloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<decimal?> LatestScheduleModifierAsync(Guid quoteId, CancellationToken ct)
        => await _db.Set<QuoteRatingSnapshot>()
            .Where(s => s.QuoteId == quoteId && !s.IsDeleted)
            .OrderByDescending(s => s.RatedAt)
            .Select(s => (decimal?)s.ScheduleModifier)
            .FirstOrDefaultAsync(ct);

    private static ControlEvaluationContext BuildQuoteContext(Quote quote, decimal? scheduleModifier)
    {
        var submission = quote.Submission;
        return BuildContext(
            quote.PremiumAmount,
            quote.TotalPremium,
            quote.IsFilingState,
            quote.LineOfBusiness,
            submission,
            quote.UWWriteup?.PayloadJson,
            scheduleModifier);
    }

    private static ControlEvaluationContext BuildPolicyContext(Policy policy, decimal? scheduleModifier)
    {
        var quote = policy.BoundQuote;
        return BuildContext(
            policy.PremiumAmount,
            policy.TotalPremium,
            quote?.IsFilingState ?? false,
            policy.LineOfBusiness,
            policy.Submission,
            quote?.UWWriteup?.PayloadJson,
            scheduleModifier);
    }

    private static ControlEvaluationContext BuildContext(
        decimal premiumAmount,
        decimal totalPremium,
        bool isFilingState,
        PolicyLineOfBusiness lineOfBusiness,
        Submission submission,
        string? writeupPayloadJson,
        decimal? scheduleModifier)
    {
        var totalInsuredValue = submission.Equipment.Where(e => !e.IsDeleted).Sum(e => e.Value ?? 0m);
        var largestSingleItemValue = submission.Equipment.Where(e => !e.IsDeleted).Select(e => e.Value ?? 0m).DefaultIfEmpty(0m).Max();
        var vehicleCount = submission.Vehicles.Count(v => !v.IsDeleted);
        var driverCount = submission.Drivers.Count(d => !d.IsDeleted);
        var glCoverages = submission.GLCoverages;
        var glClassifications = submission.GLClassifications.Where(c => !c.IsDeleted).ToList();
        var glTotalExposure = glClassifications.Sum(c => c.Exposure ?? 0m);
        var glMaxClassExposure = glClassifications.Select(c => c.Exposure ?? 0m).DefaultIfEmpty(0m).Max();
        var lossPaidReserved = submission.LossYears
            .Where(y => !y.IsDeleted)
            .SelectMany(y => y.Claims.Where(c => !c.IsDeleted))
            .Sum(c => c.Paid + c.Reserved);
        var lossPremium = submission.LossYears.Where(y => !y.IsDeleted).Sum(y => y.PremiumAmount);
        var lossRatio = lossPremium > 0 ? Math.Round(lossPaidReserved / lossPremium, 4) : 0m;

        var glClassCodes = glClassifications
            .Select(c => c.ClassCode)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!.Trim())
            .ToList();
        var supportedClassCodes = new HashSet<string>(
            ["97111", "99793", "43822", "49451", "61226", "61224", "91581", "91590", "94007", "95410", "58873", "59738"],
            StringComparer.OrdinalIgnoreCase);
        var glHasUnsupportedClassCode = glClassCodes.Any(c => !supportedClassCodes.Contains(c));
        var scheduleCreditPercent = scheduleModifier is < 1m
            ? Math.Round((1m - scheduleModifier.Value) * 100m, 2)
            : 0m;
        var writeupPayload = ParseWriteupPayload(writeupPayloadJson);

        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["premiumAmount"] = premiumAmount,
            ["totalPremium"] = totalPremium,
            ["totalInsuredValue"] = totalInsuredValue,
            ["largestSingleItemValue"] = largestSingleItemValue,
            ["vehicleCount"] = vehicleCount,
            ["driverCount"] = driverCount,
            ["lossRatio"] = lossRatio,
            ["isFilingState"] = isFilingState ? 1m : 0m,
            ["glGeneralAggregate"] = glCoverages?.GeneralAggregate ?? 0m,
            ["glProductsCompletedOps"] = glCoverages?.ProductsCompletedOps ?? 0m,
            ["glEachOccurrence"] = glCoverages?.EachOccurrence ?? 0m,
            ["glPersonalAndAdvertisingInjury"] = glCoverages?.PersonalAndAdvInjury ?? 0m,
            ["glDamageToRentedPremises"] = glCoverages?.DamageToRentedPremises ?? 0m,
            ["glMedicalExpense"] = glCoverages?.MedicalExpense ?? 0m,
            ["glTotalSubcontractorCost"] = glCoverages?.TotalSubcontractorCost ?? 0m,
            ["glAdditionalInsuredCount"] = glCoverages?.AiIndividualCount ?? 0m,
            ["glBlanketAdditionalInsured"] = glCoverages?.AiBlanket == true ? 1m : 0m,
            ["glWaiverOfSubrogationCount"] = glCoverages?.WosIndividualCount ?? 0m,
            ["glBlanketWaiverOfSubrogation"] = glCoverages?.WosBlanket == true ? 1m : 0m,
            ["glPrimaryNonContributory"] = glCoverages?.PrimaryNonContributory == true ? 1m : 0m,
            ["glIncludeTria"] = glCoverages?.IncludeTria == true ? 1m : 0m,
            ["glClassificationCount"] = glClassifications.Count,
            ["glTotalExposure"] = glTotalExposure,
            ["glMaxClassExposure"] = glMaxClassExposure,
            ["glHasUnsupportedClassCode"] = glHasUnsupportedClassCode ? 1m : 0m,
            ["glScheduleCreditPercent"] = scheduleCreditPercent,
            ["glLoggingRevenuePercent"] = writeupPayload?.GlLoggingRevenuePercent ?? 0m,
            ["glManagementExperienceYears"] = writeupPayload?.GlManagementExperienceYears ?? submission.Insured.YearsInBusiness ?? 0m,
            ["glLargestSingleLossAmount"] = writeupPayload?.GlLargestSingleLossAmount ?? 0m,
            ["glFuelStorageOverMax"] = writeupPayload?.GlFuelStorageOverMax == true ? 1m : 0m,
            ["glLogRoadBuildingOverAllowed"] = writeupPayload?.GlLogRoadBuildingOverAllowed == true ? 1m : 0m,
            ["glGradingExcavationOverAllowed"] = writeupPayload?.GlGradingExcavationOverAllowed == true ? 1m : 0m,
            ["glAircraftOrDroneOps"] = writeupPayload?.GlAircraftOrDroneOps == true ? 1m : 0m,
            ["glExplosivesUsed"] = writeupPayload?.GlExplosivesUsed == true ? 1m : 0m,
            ["glNonMechanizedLogging"] = writeupPayload?.GlNonMechanizedLogging == true ? 1m : 0m,
            ["glBankruptcyOrReceivership"] = writeupPayload?.GlBankruptcyOrReceivership == true ? 1m : 0m,
            ["glHerbicidePesticideApplication"] = writeupPayload?.GlHerbicidePesticideApplication == true ? 1m : 0m,
            ["glCraneUseOutsideAllowed"] = writeupPayload?.GlCraneUseOutsideAllowed == true ? 1m : 0m,
            ["glEquipmentRentalToOthers"] = writeupPayload?.GlEquipmentRentalToOthers == true ? 1m : 0m,
            ["glThirdPartyEquipmentRepair"] = writeupPayload?.GlThirdPartyEquipmentRepair == true ? 1m : 0m,
            ["glRightOfWayClearing"] = writeupPayload?.GlRightOfWayClearing == true ? 1m : 0m
        };

        var textValues = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["glClassCodes"] = glClassCodes
        };

        var snapshot = new
        {
            premiumAmount,
            totalPremium,
            totalInsuredValue,
            largestSingleItemValue,
            vehicleCount,
            driverCount,
            lossRatio,
            isFilingState,
            glGeneralAggregate = glCoverages?.GeneralAggregate ?? 0m,
            glProductsCompletedOps = glCoverages?.ProductsCompletedOps ?? 0m,
            glEachOccurrence = glCoverages?.EachOccurrence ?? 0m,
            glPersonalAndAdvertisingInjury = glCoverages?.PersonalAndAdvInjury ?? 0m,
            glDamageToRentedPremises = glCoverages?.DamageToRentedPremises ?? 0m,
            glMedicalExpense = glCoverages?.MedicalExpense ?? 0m,
            glTotalSubcontractorCost = glCoverages?.TotalSubcontractorCost ?? 0m,
            glAdditionalInsuredCount = glCoverages?.AiIndividualCount ?? 0,
            glBlanketAdditionalInsured = glCoverages?.AiBlanket == true,
            glWaiverOfSubrogationCount = glCoverages?.WosIndividualCount ?? 0,
            glBlanketWaiverOfSubrogation = glCoverages?.WosBlanket == true,
            glPrimaryNonContributory = glCoverages?.PrimaryNonContributory == true,
            glIncludeTria = glCoverages?.IncludeTria == true,
            glClassificationCount = glClassifications.Count,
            glTotalExposure,
            glMaxClassExposure,
            glClassCodes,
            glHasUnsupportedClassCode,
            glScheduleCreditPercent = scheduleCreditPercent,
            glLoggingRevenuePercent = writeupPayload?.GlLoggingRevenuePercent ?? 0m,
            glManagementExperienceYears = writeupPayload?.GlManagementExperienceYears ?? submission.Insured.YearsInBusiness ?? 0m,
            glLargestSingleLossAmount = writeupPayload?.GlLargestSingleLossAmount ?? 0m,
            glFuelStorageOverMax = writeupPayload?.GlFuelStorageOverMax == true,
            glLogRoadBuildingOverAllowed = writeupPayload?.GlLogRoadBuildingOverAllowed == true,
            glGradingExcavationOverAllowed = writeupPayload?.GlGradingExcavationOverAllowed == true,
            glAircraftOrDroneOps = writeupPayload?.GlAircraftOrDroneOps == true,
            glExplosivesUsed = writeupPayload?.GlExplosivesUsed == true,
            glNonMechanizedLogging = writeupPayload?.GlNonMechanizedLogging == true,
            glBankruptcyOrReceivership = writeupPayload?.GlBankruptcyOrReceivership == true,
            glHerbicidePesticideApplication = writeupPayload?.GlHerbicidePesticideApplication == true,
            glCraneUseOutsideAllowed = writeupPayload?.GlCraneUseOutsideAllowed == true,
            glEquipmentRentalToOthers = writeupPayload?.GlEquipmentRentalToOthers == true,
            glThirdPartyEquipmentRepair = writeupPayload?.GlThirdPartyEquipmentRepair == true,
            glRightOfWayClearing = writeupPayload?.GlRightOfWayClearing == true,
            lineOfBusiness = lineOfBusiness.ToString(),
            state = NormalizeState(submission.Insured.State)
        };

        return new ControlEvaluationContext(values, textValues, snapshot);
    }

    private static IReadOnlyList<UnderwritingControlStage> StagesFor(UnderwritingControlStage stage) =>
        stage == UnderwritingControlStage.Bind ? BindLikeStages : [stage];

    private static string NormalizeState(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode))
            return "ALL";
        var trimmed = stateCode.Trim().ToUpperInvariant();
        return trimmed is "*" or "ALL" ? "ALL" : trimmed;
    }

    private static UnderwritingControlEnforcementResultDto Map(UnderwritingControlEnforcementResult result) =>
        new(
            result.Id,
            result.GuidelineControlId,
            result.TargetType,
            result.TargetId,
            result.Stage,
            result.Status,
            result.IsBlocking,
            result.OverrideAllowed,
            result.OverridePermission,
            result.Message,
            result.GuidelineControl.RuleKey,
            result.GuidelineControl.Label,
            result.GuidelineControl.SourceCitation,
            result.ConditionJson,
            result.InputSnapshotJson,
            result.EvaluatedAt,
            result.OverriddenByUserId,
            result.OverriddenAt,
            result.OverrideReason);

    private sealed record ControlEvaluationContext(
        IReadOnlyDictionary<string, decimal> Values,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> TextValues,
        object Snapshot);
    private sealed record ControlEvaluation(UnderwritingControlEvaluationStatus Status, string Message);
    private sealed record ConditionRule(string Field, string Operator, JsonElement Value);
}
