using SIMS.Domain.Entities.Rating;

namespace SIMS.Application.Rating;

/// <summary>
/// Pure APD_v1 rating calculation — no I/O, no EF.
///
/// Formula per vehicle (two coverages):
///   CompPrem = (StatedValue/100) × COMP_BASE_RATE[vehicleClass, valueBracket]
///                                 × MILEAGE_FACTOR[roadType, mileageClass]
///                                 × DRIVER_FACTOR[driverAgeCode, driverPointsCode]
///                                 × driverExpMod
///                                 × OPERATION_FACTOR[operationCode, vehicleClass]
///                                 × STATE_FACTOR[state, operationCode]
///                                 × COMP_DED_FACTOR[compDeductible, valueBracket]
///                                 × scheduleModifier
///   CollPrem = same with COLL_BASE_RATE and COLL_DED_FACTOR
///   VehicleTotal = CompPrem + CollPrem
/// </summary>
public static class ApdV1Formula
{
    public record VehicleInput(
        int UnitNumber,
        int VehicleClass,       // 1=Light/Med, 2=Heavy/XHeavy, 3=TT, 4=Trailer
        int RoadType,           // 1–5
        int MileageClass,       // 10/11/12/13/20 (derived from AnnualMiles by caller)
        int OperationCode,      // 91/92/99
        string State,           // "AL", "AR", etc.
        decimal StatedValue,
        decimal CompDeductible,
        decimal CollDeductible,
        int DriverAgeCode,      // 0–8
        int DriverPointsCode,   // 0–5 (5 = fleet unassigned)
        decimal DriverExpMod    // 1.0 / 1.15 / 1.25
    );

    public record RatingLine(
        string ExposureRef,
        string ValueBracket,
        string MileageClassKey,
        decimal CompBaseRate,
        decimal CollBaseRate,
        decimal MilageFactor,
        decimal DriverFactor,
        decimal DriverExpMod,
        decimal OperationFactor,
        decimal StateFactor,
        decimal CompDedFactor,
        decimal CollDedFactor,
        decimal CompPremium,
        decimal CollPremium,
        decimal TotalPremium
    );

    public record RatingResult(
        IReadOnlyList<RatingLine> Lines,
        decimal TotalCompPremium,
        decimal TotalCollPremium,
        decimal ManualPremium,
        decimal GrandTotal
    );

    // ── Public helpers ────────────────────────────────────────────────────────

    /// <summary>Maps stated value to value bracket code 1–8.</summary>
    public static string ValueBracket(decimal statedValue) => statedValue switch
    {
        <= 10_000m  => "1",
        <= 20_000m  => "2",
        <= 30_000m  => "3",
        <= 45_000m  => "4",
        <= 60_000m  => "5",
        <= 80_000m  => "6",
        <= 100_000m => "7",
        _           => "8",
    };

    /// <summary>Maps annual miles to mileage class code 10/11/12/13/20.</summary>
    public static string MileageClassKey(int? annualMiles) => annualMiles switch
    {
        null or 0    => "10",
        <= 12_500    => "11",
        <= 45_000    => "12",
        <= 80_000    => "13",
        _            => "20",
    };

    public static decimal? LookupFactor(FactorTable table, Dictionary<string, string> dims)
        => table.Rows.FirstOrDefault(r =>
                dims.All(kv => r.DimensionValues.TryGetValue(kv.Key, out var v) && v == kv.Value))
            ?.Factor;

    // ── Main entry ────────────────────────────────────────────────────────────

    public static RatingResult Rate(
        FactorTable compBaseRateTable,
        FactorTable collBaseRateTable,
        FactorTable mileageTable,
        FactorTable driverTable,
        FactorTable operationTable,
        FactorTable stateTable,
        FactorTable compDedTable,
        FactorTable collDedTable,
        IReadOnlyList<VehicleInput> vehicles,
        decimal modifier,
        decimal? minimumPremium = null)
    {
        var lines = new List<RatingLine>();
        decimal totalComp = 0, totalColl = 0;

        foreach (var v in vehicles)
        {
            var vcStr  = v.VehicleClass.ToString();
            var rtStr  = v.RoadType.ToString();
            var mlStr  = v.MileageClass.ToString();
            var opStr  = v.OperationCode.ToString();
            var vbStr  = ValueBracket(v.StatedValue);
            var cdcStr = ((int)v.CompDeductible).ToString();
            var cdlStr = ((int)v.CollDeductible).ToString();
            var agStr  = v.DriverAgeCode.ToString();
            var ptStr  = v.DriverPointsCode.ToString();

            var compBase = LookupFactor(compBaseRateTable,
                new() { ["vehicle_class"] = vcStr, ["value_bracket"] = vbStr })
                ?? throw new InvalidOperationException(
                    $"No comp base rate for vehicle_class={vcStr}, value_bracket={vbStr}");

            var collBase = LookupFactor(collBaseRateTable,
                new() { ["vehicle_class"] = vcStr, ["value_bracket"] = vbStr })
                ?? throw new InvalidOperationException(
                    $"No coll base rate for vehicle_class={vcStr}, value_bracket={vbStr}");

            var mileageFactor = LookupFactor(mileageTable,
                new() { ["road_type"] = rtStr, ["mileage_class"] = mlStr })
                ?? throw new InvalidOperationException(
                    $"No mileage factor for road_type={rtStr}, mileage_class={mlStr}");

            var driverFactor = LookupFactor(driverTable,
                new() { ["driver_age"] = agStr, ["driver_points"] = ptStr })
                ?? throw new InvalidOperationException(
                    $"No driver factor for driver_age={agStr}, driver_points={ptStr}");

            var operationFactor = LookupFactor(operationTable,
                new() { ["operation_code"] = opStr, ["vehicle_class"] = vcStr })
                ?? throw new InvalidOperationException(
                    $"No operation factor for operation_code={opStr}, vehicle_class={vcStr}");

            var stateFactor = LookupFactor(stateTable,
                new() { ["state"] = v.State, ["operation_code"] = opStr })
                ?? throw new InvalidOperationException(
                    $"No state factor for state={v.State}, operation_code={opStr}");

            var compDedFactor = LookupFactor(compDedTable,
                new() { ["deductible"] = cdcStr, ["value_bracket"] = vbStr })
                ?? throw new InvalidOperationException(
                    $"No comp deductible factor for deductible={cdcStr}, value_bracket={vbStr}");

            var collDedFactor = LookupFactor(collDedTable,
                new() { ["deductible"] = cdlStr, ["value_bracket"] = vbStr })
                ?? throw new InvalidOperationException(
                    $"No coll deductible factor for deductible={cdlStr}, value_bracket={vbStr}");

            var sa100 = v.StatedValue / 100m;
            var shared = mileageFactor * driverFactor * v.DriverExpMod * operationFactor * stateFactor * modifier;

            var compPrem = Math.Round(sa100 * compBase * shared * compDedFactor, 2);
            var collPrem = Math.Round(sa100 * collBase * shared * collDedFactor, 2);
            var total    = compPrem + collPrem;

            totalComp += compPrem;
            totalColl += collPrem;

            lines.Add(new RatingLine(
                ExposureRef:     $"VEH-{v.UnitNumber:D3}",
                ValueBracket:    vbStr,
                MileageClassKey: mlStr,
                CompBaseRate:    compBase,
                CollBaseRate:    collBase,
                MilageFactor:    mileageFactor,
                DriverFactor:    driverFactor,
                DriverExpMod:    v.DriverExpMod,
                OperationFactor: operationFactor,
                StateFactor:     stateFactor,
                CompDedFactor:   compDedFactor,
                CollDedFactor:   collDedFactor,
                CompPremium:     compPrem,
                CollPremium:     collPrem,
                TotalPremium:    total
            ));
        }

        var manual = totalComp + totalColl;
        var grand  = minimumPremium.HasValue ? Math.Max(manual, minimumPremium.Value) : manual;
        return new RatingResult(lines, totalComp, totalColl, manual, grand);
    }
}
