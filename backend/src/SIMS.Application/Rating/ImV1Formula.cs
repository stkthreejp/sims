using SIMS.Domain.Entities.Rating;

namespace SIMS.Application.Rating;

/// <summary>
/// Pure IM_v1 rating calculation — no I/O, no EF. Called by RatingEngineService and test harness.
/// </summary>
public static class ImV1Formula
{
    public record EquipmentInput(int TypeNumber, int? Year, decimal Value, decimal? Deductible);

    public record RatingLine(
        string ExposureRef,
        decimal BaseRate,
        decimal DeductibleFactor,
        string AgeBand,
        string DeductibleKey,
        decimal LinePremium
    );

    public record RatingResult(
        IReadOnlyList<RatingLine> Lines,
        decimal ManualPremium,
        decimal GrandTotal
    );

    public static string AgeBand(int? year, int effectiveYear)
    {
        if (year is null) return "1-3";
        var age = effectiveYear - year.Value;
        return age switch
        {
            <= 3 => "1-3",
            <= 7 => "4-7",
            <= 11 => "8-11",
            _ => "12+"
        };
    }

    public static string DeductibleKey(decimal? deductible)
        => deductible.HasValue ? ((int)deductible.Value).ToString() : "10%ACV";

    public static decimal? LookupFactor(FactorTable table, Dictionary<string, string> dims)
        => table.Rows.FirstOrDefault(r =>
                dims.All(kv => r.DimensionValues.TryGetValue(kv.Key, out var v) && v == kv.Value))
            ?.Factor;

    public static RatingResult Rate(
        FactorTable baseRateTable,
        FactorTable deductibleTable,
        IReadOnlyList<EquipmentInput> items,
        int effectiveYear,
        decimal modifier,
        decimal? minimumPremium = null)
    {
        var lines = new List<RatingLine>();
        decimal manualPremium = 0;

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var typeNum = item.TypeNumber.ToString();
            var ageBand = AgeBand(item.Year, effectiveYear);
            var dedKey = DeductibleKey(item.Deductible);

            var baseRate = LookupFactor(baseRateTable, new() { ["equipment_type"] = typeNum, ["age_band"] = ageBand })
                ?? throw new InvalidOperationException($"No base rate for type={typeNum} age_band={ageBand}");

            var deductibleFactor = LookupFactor(deductibleTable, new() { ["equipment_type"] = typeNum, ["deductible"] = dedKey })
                ?? throw new InvalidOperationException($"No deductible factor for type={typeNum} deductible={dedKey}");

            var linePremium = Math.Round(item.Value / 100m * baseRate * deductibleFactor * modifier, 2);
            manualPremium += linePremium;

            lines.Add(new RatingLine(
                ExposureRef: $"EQ-{i + 1:D3}",
                BaseRate: baseRate,
                DeductibleFactor: deductibleFactor,
                AgeBand: ageBand,
                DeductibleKey: dedKey,
                LinePremium: linePremium
            ));
        }

        var grandTotal = minimumPremium.HasValue
            ? Math.Max(manualPremium, minimumPremium.Value)
            : manualPremium;

        return new RatingResult(lines, manualPremium, grandTotal);
    }
}
