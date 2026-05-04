using SIMS.Domain.Entities.Rating;
using SIMS.Domain.Enums;

namespace SIMS.Application.Rating.Tests.Helpers;

/// <summary>
/// In-memory IM v1 factor tables built from the exact values in the seed migration.
/// Used by tests to avoid any database dependency.
/// </summary>
internal static class ImV1FactorTables
{
    public static FactorTable BaseRate { get; } = BuildBaseRate();
    public static FactorTable DeductibleFactor { get; } = BuildDeductibleFactor();

    private static FactorTable BuildBaseRate()
    {
        // Source: 20260503230000_Rating_IM_Seed.cs — PART 4
        var rows = new (int type, string ageBand, decimal factor)[]
        {
            (1, "1-3",  1.140m), (1, "4-7",  1.710m), (1, "8-11", 1.710m), (1, "12+", 2.166m),
            (2, "1-3",  0.500m), (2, "4-7",  0.500m), (2, "8-11", 0.550m), (2, "12+", 0.600m),
            (3, "1-3",  0.500m), (3, "4-7",  0.500m), (3, "8-11", 0.550m), (3, "12+", 0.600m),
            (4, "1-3",  1.140m), (4, "4-7",  1.710m), (4, "8-11", 1.710m), (4, "12+", 2.166m),
            (5, "1-3",  0.720m), (5, "4-7",  1.080m), (5, "8-11", 1.080m), (5, "12+", 1.296m),
            (6, "1-3",  1.640m), (6, "4-7",  2.460m), (6, "8-11", 2.460m), (6, "12+", 2.952m),
            (7, "1-3",  1.000m), (7, "4-7",  1.000m), (7, "8-11", 1.000m), (7, "12+", 1.000m),
            (8, "1-3",  0.620m), (8, "4-7",  0.650m), (8, "8-11", 0.650m), (8, "12+", 0.700m),
            (9, "1-3",  1.640m), (9, "4-7",  2.460m), (9, "8-11", 2.460m), (9, "12+", 2.952m),
            (10, "1-3", 0.520m), (10, "4-7", 0.550m), (10, "8-11", 0.570m), (10, "12+", 0.620m),
            (11, "1-3", 0.620m), (11, "4-7", 0.650m), (11, "8-11", 0.650m), (11, "12+", 0.700m),
            (12, "1-3", 0.500m), (12, "4-7", 0.500m), (12, "8-11", 0.550m), (12, "12+", 0.600m),
        };

        return new FactorTable
        {
            Id = Guid.NewGuid(),
            Code = "BASE_RATE",
            DimensionNames = ["equipment_type", "age_band"],
            ValueSemantics = FactorKind.RatePer100,
            Rows = rows.Select(r => new FactorRow
            {
                Id = Guid.NewGuid(),
                DimensionValues = new Dictionary<string, string>
                {
                    ["equipment_type"] = r.type.ToString(),
                    ["age_band"] = r.ageBand,
                },
                Factor = r.factor,
            }).ToList(),
        };
    }

    private static FactorTable BuildDeductibleFactor()
    {
        // Source: 20260503230000_Rating_IM_Seed.cs — PART 5
        var rows = new (int type, string ded, decimal factor)[]
        {
            (1,  "2500",   1.000m), (1,  "5000",  0.980m), (1,  "10000", 0.960m), (1,  "25000", 0.940m), (1,  "10%ACV", 0.880m),
            (2,  "2500",   1.000m), (2,  "5000",  0.980m), (2,  "10000", 0.960m), (2,  "25000", 0.940m), (2,  "10%ACV", 0.880m),
            (3,  "2500",   1.000m), (3,  "5000",  0.980m), (3,  "10000", 0.960m), (3,  "25000", 0.920m), (3,  "10%ACV", 0.880m),
            (4,  "2500",   1.020m), (4,  "5000",  1.000m), (4,  "10000", 0.980m), (4,  "25000", 0.960m), (4,  "10%ACV", 0.920m),
            (5,  "2500",   1.000m), (5,  "5000",  0.980m), (5,  "10000", 0.960m), (5,  "25000", 0.940m), (5,  "10%ACV", 0.880m),
            (6,  "2500",   0.000m), (6,  "5000",  1.000m), (6,  "10000", 0.980m), (6,  "25000", 0.960m), (6,  "10%ACV", 0.920m),
            (7,  "2500",   1.000m), (7,  "5000",  0.980m), (7,  "10000", 0.960m), (7,  "25000", 0.920m), (7,  "10%ACV", 0.880m),
            (8,  "2500",   1.000m), (8,  "5000",  0.980m), (8,  "10000", 0.960m), (8,  "25000", 0.920m), (8,  "10%ACV", 0.880m),
            (9,  "2500",   0.000m), (9,  "5000",  1.000m), (9,  "10000", 0.980m), (9,  "25000", 0.960m), (9,  "10%ACV", 0.920m),
            (10, "2500",   1.000m), (10, "5000",  0.980m), (10, "10000", 0.960m), (10, "25000", 0.920m), (10, "10%ACV", 0.880m),
            (11, "2500",   1.000m), (11, "5000",  0.980m), (11, "10000", 0.960m), (11, "25000", 0.920m), (11, "10%ACV", 0.880m),
            (12, "2500",   1.000m), (12, "5000",  0.980m), (12, "10000", 0.960m), (12, "25000", 0.920m), (12, "10%ACV", 0.880m),
        };

        return new FactorTable
        {
            Id = Guid.NewGuid(),
            Code = "DEDUCTIBLE_FACTOR",
            DimensionNames = ["equipment_type", "deductible"],
            ValueSemantics = FactorKind.Multiplier,
            Rows = rows.Select(r => new FactorRow
            {
                Id = Guid.NewGuid(),
                DimensionValues = new Dictionary<string, string>
                {
                    ["equipment_type"] = r.type.ToString(),
                    ["deductible"] = r.ded,
                },
                Factor = r.factor,
            }).ToList(),
        };
    }
}
