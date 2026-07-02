namespace SIMS.Application.Rating;

/// <summary>
/// Pure GL_v2 rating calculation — no I/O, no EF. Data-driven successor to GL_v1.
///
/// Longleaf® GL, Brace Program, ISO Commercial Lines Manual Division Six GL,
/// Sublines 334 (Prem/Ops) and 336 (Prod/Comp-Ops). Unlike GL_v1 (hardcoded
/// Alabama rates), GL_v2 is <b>state-driven</b>: all loss costs, ILFs, class
/// config and program params are supplied in <see cref="RateData"/>, which the
/// engine loads from the plan version's factor tables. Rate changes are data
/// edits, not code changes.
///
/// Per classification line (mirrors SMM_GL_Rater_Finalv11.xlsx):
///   CoRate334 = LossCost334[class, state] × LCM
///   CoRate336 = LossCost336[class, state] × LCM      (only if class has P/CO)
///   BasePO    = CoRate334 × Exposure / Divisor
///   BasePCO   = CoRate336 × Exposure / Divisor
///   RatedPO   = BasePO  × ILF[occLimit, class P/O tier]
///   RatedPCO  = BasePCO × ILF[pcoAgg,  class P/CO tier]
///   LineTotal = RatedPO + RatedPCO
///
/// Account level:
///   Subtotal        = Σ LineTotals                       (unrounded)
///   ModifiedPremium = ROUND(Subtotal × scheduleMod, 0)   (whole dollar)
///   Endorsements    = Logging &amp; Lumbering only (AI/WOS/PNC are priced by the
///                     global additional-interest engine, not here)
///   TRIA            = IncludeTria ? ROUND(ModifiedPremium × TriaRate, 0) : 0
///   GrandTotal      = ModifiedPremium + Endorsements + TRIA
///
/// A class/state combination with no filed P/O loss cost (an "(a)" cell in the
/// workbook) is treated as refer-to-company (throws), not a $0 line.
/// No medical-payments ILF is applied (v11 folds medical into the base cost;
/// limits above the referral threshold are handled upstream as referrals).
/// </summary>
public static class GlV2Formula
{
    public const string FormulaKey = "GL_v2";

    // Tier keys used by the ILF table (and each class's P/O and P/CO tier).
    public static readonly string[] IlfTiers = ["PO_T1", "PO_T2", "PO_T3", "PCO_TA", "PCO_TB", "PCO_TC"];

    public sealed record ClassConfig(
        string Code, string Description, string PremiumBasis,
        bool HasPco, string PoTier, string PcoTier, int Divisor);

    /// <summary>
    /// All rate data for one GL_v2 plan version (loaded from factor tables).
    /// Additional-interest charges (AI/WOS/PNC) are intentionally absent — those are
    /// owned by the global additional-interest engine (CarrierAdditionalInterestRate),
    /// not this formula. This rater covers only the classification premium plus the
    /// GL-specific TRIA surcharge and Logging &amp; Lumbering endorsement.
    /// </summary>
    public sealed record RateData(
        decimal Lcm,
        decimal TriaRate,
        IReadOnlyDictionary<string, ClassConfig> Classes,
        IReadOnlyDictionary<(string ClassCode, string State), decimal> LossCost334,
        IReadOnlyDictionary<(string ClassCode, string State), decimal> LossCost336,
        IReadOnlyDictionary<(int Limit, string Tier), decimal> Ilf,
        // Logging & Lumbering endorsement (class 97111): limit → (minimum premium, % of 97111 premium)
        IReadOnlyDictionary<int, (decimal Min, decimal Pct)> LoggingLumbering);

    public sealed record ClassInput(string ClassCode, decimal Exposure);

    public sealed record RatingLine(
        string ClassCode, string Description, string PremiumBasis, decimal Exposure,
        decimal CoRate334, decimal CoRate336, decimal IlfPo, decimal IlfPco,
        decimal BasePo, decimal BasePco, decimal RatedPo, decimal RatedPco, decimal LineTotal);

    public sealed record EndorsementLine(string Code, string Label, decimal Premium);

    public sealed record RatingResult(
        IReadOnlyList<RatingLine> Lines,
        IReadOnlyList<EndorsementLine> Endorsements,
        decimal Subtotal,
        decimal ScheduleModifier,
        decimal ModifiedPremium,
        decimal EndorsementTotal,
        decimal TriaPremium,
        decimal ManualPremium,   // = Subtotal (pre-modifier)
        decimal GrandTotal);

    public static RatingResult Rate(
        RateData data,
        string state,
        IReadOnlyList<ClassInput> classifications,
        int occLimit,
        int pcoAggregate,
        decimal scheduleModifier,
        bool includeTria,
        decimal? loggingLumberingLimit = null)
    {
        var st = (state ?? string.Empty).Trim().ToUpperInvariant();
        if (st.Length == 0)
            throw new InvalidOperationException("A rating state is required for GL rating.");

        var lines = new List<RatingLine>();
        decimal subtotal = 0m;

        foreach (var input in classifications)
        {
            if (!data.Classes.TryGetValue(input.ClassCode, out var cd))
                throw new InvalidOperationException($"Unknown class code: {input.ClassCode}");

            if (!data.LossCost334.TryGetValue((cd.Code, st), out var lc334))
                throw new InvalidOperationException(
                    $"Class {cd.Code} has no filed rate in {st} — refer to company.");

            decimal coRate334 = lc334 * data.Lcm;
            decimal exposureUnits = input.Exposure / cd.Divisor;
            decimal basePo = coRate334 * exposureUnits;

            if (!data.Ilf.TryGetValue((occLimit, cd.PoTier), out var ilfPo))
                throw new InvalidOperationException(
                    $"No P/O ILF for occurrence limit {occLimit:N0} (tier {cd.PoTier}).");
            decimal ratedPo = basePo * ilfPo;

            decimal coRate336 = 0m, basePco = 0m, ilfPco = 0m, ratedPco = 0m;
            if (cd.HasPco && data.LossCost336.TryGetValue((cd.Code, st), out var lc336))
            {
                coRate336 = lc336 * data.Lcm;
                basePco = coRate336 * exposureUnits;
                if (!data.Ilf.TryGetValue((pcoAggregate, cd.PcoTier), out ilfPco))
                    throw new InvalidOperationException(
                        $"No P/CO ILF for aggregate {pcoAggregate:N0} (tier {cd.PcoTier}).");
                ratedPco = basePco * ilfPco;
            }

            decimal lineTotal = ratedPo + ratedPco;
            subtotal += lineTotal;

            lines.Add(new RatingLine(
                cd.Code, cd.Description, cd.PremiumBasis, input.Exposure,
                coRate334, coRate336, ilfPo, ilfPco, basePo, basePco, ratedPo, ratedPco, lineTotal));
        }

        decimal modifiedPremium = Math.Round(subtotal * scheduleModifier, 0, MidpointRounding.AwayFromZero);

        // The only endorsement this formula prices is Logging & Lumbering (class 97111);
        // AI/WOS/PNC are priced by the global additional-interest engine.
        var endLines = new List<EndorsementLine>();

        // Logging & Lumbering endorsement (class 97111 only): greater of the flat
        // minimum for the selected limit or a % of the 97111 line premium.
        if (loggingLumberingLimit is > 0m)
        {
            var llLimit = (int)loggingLumberingLimit.Value;
            if (!data.LoggingLumbering.TryGetValue(llLimit, out var ll))
                throw new InvalidOperationException(
                    $"Unsupported Logging & Lumbering endorsement limit: {llLimit:N0}.");
            decimal logging97111 = lines.Where(l => l.ClassCode == "97111").Sum(l => l.LineTotal);
            decimal llCharge = Math.Max(ll.Min, logging97111 * ll.Pct);
            if (llCharge != 0m)
                endLines.Add(new EndorsementLine("LL", "Logging & Lumbering Endorsement", llCharge));
        }

        decimal endorsementTotal = endLines.Sum(e => e.Premium);

        decimal triaPremium = includeTria
            ? Math.Round(modifiedPremium * data.TriaRate, 0, MidpointRounding.AwayFromZero)
            : 0m;

        decimal grandTotal = modifiedPremium + endorsementTotal + triaPremium;

        return new RatingResult(
            Lines: lines,
            Endorsements: endLines,
            Subtotal: subtotal,
            ScheduleModifier: scheduleModifier,
            ModifiedPremium: modifiedPremium,
            EndorsementTotal: endorsementTotal,
            TriaPremium: triaPremium,
            ManualPremium: subtotal,
            GrandTotal: grandTotal);
    }
}
