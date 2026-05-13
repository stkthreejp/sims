namespace SIMS.Application.Rating;

/// <summary>
/// Pure GL_v1 rating calculation — no I/O, no EF.
///
/// Brace Program — Longleaf® GL, Sublines 334 (P/O) and 336 (P/CO).
/// ISO Commercial Lines Manual, Division Six GL. Alabama state rates.
///
/// Per classification line:
///   ExposureUnits = Exposure / Divisor
///   BasePO  = ExposureUnits × CoRate334  (CoRate = ISO_LC × LCM 1.65)
///   BasePCO = ExposureUnits × CoRate336  (zero if class has no P/CO)
///   RatedPO  = BasePO  × ILF_PO[occLimit]  × MedILF[classGroup, medLimit]
///   RatedPCO = BasePCO × ILF_PCO[pcoLimit]
///   LineTotal = RatedPO + RatedPCO
///
/// Account level:
///   Subtotal        = sum(LineTotals)
///   ModifiedPremium = Subtotal × scheduleModifier   (0.80–1.20)
///   TRIA            = IncludeTria ? ModifiedPremium × 2.5% : 0
///   GrandTotal      = ModifiedPremium + TRIA
/// </summary>
public static class GlV1Formula
{
    // ── Constants ─────────────────────────────────────────────────────────────

    private const decimal LCM = 1.65m;

    // ── ILF Table (ISO Rule 56, Tables 56.B.1–56.B.6) ────────────────────────
    // Key = occurrence limit in dollars. Base = $100K occ / $200K agg.
    // Array: [T1, T2, T3, TA, TB, TC]  (0-indexed; see PoIlfIdx / PcoIlfIdx in ClassDef)

    private static readonly Dictionary<int, decimal[]> IlfTable = new()
    {
        [100_000]   = [1.00m, 1.00m, 1.00m, 1.00m, 1.00m, 1.00m],
        [300_000]   = [1.37m, 1.38m, 1.36m, 1.24m, 1.27m, 1.33m],
        [500_000]   = [1.54m, 1.58m, 1.57m, 1.34m, 1.40m, 1.54m],
        [1_000_000] = [1.76m, 1.87m, 1.89m, 1.46m, 1.57m, 1.85m],
        [2_000_000] = [1.97m, 2.17m, 2.28m, 1.61m, 1.75m, 2.21m],
    };

    // ── Medical ILF Table (ISO Table 23.D.3.) ─────────────────────────────────
    // Contracting class codes (90000–99999) carry slightly higher med factors.

    private enum MedGroup { General, Contracting }

    private static decimal GetMedIlf(MedGroup group, int medLimit) => (group, medLimit) switch
    {
        (_, 5_000)                       => 1.000m,
        (MedGroup.Contracting, 10_000)   => 1.011m,
        (MedGroup.Contracting, 15_000)   => 1.017m,
        (MedGroup.Contracting, 25_000)   => 1.026m,
        (_, 10_000)                      => 1.007m,
        (_, 15_000)                      => 1.011m,
        (_, 25_000)                      => 1.016m,
        _ => throw new InvalidOperationException($"Unsupported medical limit: {medLimit}")
    };

    // ── Classification Rate Table ─────────────────────────────────────────────
    // ISO Loss Costs × LCM = Company Rates. Alabama state pages (default).
    // PoIlfIdx / PcoIlfIdx: 0-based index into IlfTable row (vlookup col − 2).

    private record ClassDef(
        string Description,
        string PremiumBasis,
        int Divisor,
        bool HasPco,
        int PoIlfIdx,    // 0=T1,1=T2,2=T3
        int PcoIlfIdx,   // 3=TA,4=TB,5=TC
        decimal IsoLc334,
        decimal IsoLc336,
        MedGroup MedGroup
    );

    private static readonly Dictionary<string, ClassDef> ClassTable = new()
    {
        // Code   Description                         Basis              Div   PCO   PO   PCO   ISO334   ISO336  MedGrp
        ["97111"] = new("Logging and Lumbering",           "Payroll / $1,000",  1000, false, 1,   3,   5.640m,  0.000m, MedGroup.Contracting),
        ["99793"] = new("Truckers – Common/Contract",      "Payroll / $1,000",  1000, false, 2,   3,   3.230m,  0.000m, MedGroup.Contracting),
        ["43822"] = new("Forestry Services",               "Payroll / $1,000",  1000, false, 1,   3,   6.280m,  0.000m, MedGroup.General),
        ["49451"] = new("Vacant Land – Other",             "Each Acre",            1, false, 1,   3,   0.120m,  0.000m, MedGroup.General),
        ["61226"] = new("Buildings/Premises – Office NOC", "Area / 1,000 sq ft",1000, false, 1,   3, 120.000m,  0.000m, MedGroup.General),
        ["61224"] = new("Buildings/Premises – Office Emp", "Area / 1,000 sq ft",1000, false, 1,   3,  51.800m,  0.000m, MedGroup.General),
        ["91581"] = new("Sub-contracted Work",             "Total Cost / $1,000",1000, true,  2,   4,   0.100m,  0.100m, MedGroup.Contracting),
        ["91590"] = new("Contractors Permanent Yard",      "Payroll / $1,000",  1000, false, 2,   3,   3.670m,  0.000m, MedGroup.Contracting),
        ["94007"] = new("Excavation",                      "Payroll / $1,000",  1000, true,  1,   4,  11.700m,  4.830m, MedGroup.Contracting),
        ["95410"] = new("Grading of Land",                 "Payroll / $1,000",  1000, true,  1,   4,   4.710m,  2.710m, MedGroup.Contracting),
        ["58873"] = new("Saw Mills or Planing Mills",      "Gross Sales / $1,000",1000, true, 2,   4,   0.107m,  0.021m, MedGroup.General),
        ["59738"] = new("Tie, Post or Pole Yard",          "Gross Sales / $1,000",1000, true, 2,   4,   0.125m,  0.044m, MedGroup.General),
    };

    // ── Public API ────────────────────────────────────────────────────────────

    public static IReadOnlyCollection<string> SupportedClassCodes => ClassTable.Keys;

    public static bool TryGetClassDef(string classCode, out string description, out string premiumBasis)
    {
        if (ClassTable.TryGetValue(classCode, out var def))
        {
            description = def.Description;
            premiumBasis = def.PremiumBasis;
            return true;
        }
        description = premiumBasis = string.Empty;
        return false;
    }

    // ── Records ───────────────────────────────────────────────────────────────

    public record ClassInput(string ClassCode, decimal Exposure);

    public record RatingLine(
        string ClassCode,
        string Description,
        string PremiumBasis,
        decimal ExposureUnits,
        decimal CoRate334,
        decimal CoRate336,
        decimal IlfPo,
        decimal IlfPco,
        decimal MedIlf,
        decimal BasePo,
        decimal BasePco,
        decimal RatedPo,
        decimal RatedPco,
        decimal LineTotal
    );

    public record RatingResult(
        IReadOnlyList<RatingLine> Lines,
        decimal Subtotal,
        decimal ScheduleModifier,
        decimal ModifiedPremium,
        decimal TriaPremium,
        decimal ManualPremium,   // = Subtotal (pre-modifier)
        decimal GrandTotal
    );

    // ── Main entry ────────────────────────────────────────────────────────────

    public static RatingResult Rate(
        IReadOnlyList<ClassInput> classifications,
        int occLimit,            // 300000 / 500000 / 1000000
        int pcoLimit,            // 1000000 / 2000000
        int medLimit,            // 5000 / 10000 / 15000 / 25000
        decimal scheduleModifier,// quote-level: 0.80–1.20 (applied to Subtotal)
        bool includeTria)
    {
        if (!IlfTable.ContainsKey(occLimit))
            throw new InvalidOperationException($"Unsupported occurrence limit: {occLimit:N0}");
        if (!IlfTable.ContainsKey(pcoLimit))
            throw new InvalidOperationException($"Unsupported PCO limit: {pcoLimit:N0}");

        var lines = new List<RatingLine>();
        decimal subtotal = 0m;

        foreach (var input in classifications)
        {
            if (!ClassTable.TryGetValue(input.ClassCode, out var cd))
                throw new InvalidOperationException($"Unknown class code: {input.ClassCode}");

            decimal coRate334 = cd.IsoLc334 * LCM;
            decimal coRate336 = cd.IsoLc336 * LCM;

            decimal exposureUnits = input.Exposure / cd.Divisor;
            decimal basePo  = exposureUnits * coRate334;
            decimal basePco = cd.HasPco ? exposureUnits * coRate336 : 0m;

            decimal ilfPo  = IlfTable[occLimit][cd.PoIlfIdx];
            decimal ilfPco = cd.HasPco ? IlfTable[pcoLimit][cd.PcoIlfIdx] : 0m;
            decimal medIlf = GetMedIlf(cd.MedGroup, medLimit);

            decimal ratedPo  = Math.Round(basePo  * ilfPo  * medIlf, 2);
            decimal ratedPco = Math.Round(basePco * ilfPco, 2);
            decimal lineTotal = ratedPo + ratedPco;
            subtotal += lineTotal;

            lines.Add(new RatingLine(
                ClassCode:     input.ClassCode,
                Description:   cd.Description,
                PremiumBasis:  cd.PremiumBasis,
                ExposureUnits: exposureUnits,
                CoRate334:     coRate334,
                CoRate336:     coRate336,
                IlfPo:         ilfPo,
                IlfPco:        ilfPco,
                MedIlf:        medIlf,
                BasePo:        Math.Round(basePo, 2),
                BasePco:       Math.Round(basePco, 2),
                RatedPo:       ratedPo,
                RatedPco:      ratedPco,
                LineTotal:     lineTotal
            ));
        }

        decimal modifiedPremium = Math.Round(subtotal * scheduleModifier, 2);

        decimal triaPremium   = includeTria ? Math.Round(modifiedPremium * 0.025m, 2) : 0m;
        decimal grandTotal    = modifiedPremium + triaPremium;

        return new RatingResult(
            Lines:                lines,
            Subtotal:             subtotal,
            ScheduleModifier:     scheduleModifier,
            ModifiedPremium:      modifiedPremium,
            TriaPremium:          triaPremium,
            ManualPremium:        subtotal,
            GrandTotal:           grandTotal
        );
    }
}
