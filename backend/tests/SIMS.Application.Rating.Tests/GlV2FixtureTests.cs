using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using SIMS.Application.Rating;

namespace SIMS.Application.Rating.Tests;

/// <summary>
/// Fixture-driven parity tests for the data-driven GL_v2 rater.
/// Rate tables live in Fixtures/GL_v2/rate_data.json (extracted from
/// SMM_GL_Rater_Finalv11.xlsx); each scenario dir has inputs.json + expected.json.
/// Any fixture failure blocks merge — this is the Excel parity CI gate for GL.
/// </summary>
public class GlV2FixtureTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly Lazy<GlV2Formula.RateData> Rates = new(LoadRateData);

    // Workbook ROUND() is half-up; match it when comparing unrounded money values.
    private static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    public static IEnumerable<object[]> FixtureDirectories()
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "GL_v2");
        if (!Directory.Exists(baseDir))
            yield break;

        foreach (var dir in Directory.GetDirectories(baseDir).OrderBy(d => d))
            yield return [Path.GetFileName(dir), dir];
    }

    [Theory]
    [MemberData(nameof(FixtureDirectories))]
    public void GL_v2_fixture_matches(string fixtureName, string fixtureDir)
    {
        var inputs = JsonSerializer.Deserialize<FixtureInputs>(
            File.ReadAllText(Path.Combine(fixtureDir, "inputs.json")), JsonOpts)!;
        var expected = JsonSerializer.Deserialize<FixtureExpected>(
            File.ReadAllText(Path.Combine(fixtureDir, "expected.json")), JsonOpts)!;

        var classes = inputs.Lines
            .Select(l => new GlV2Formula.ClassInput(l.ClassCode, l.Exposure))
            .ToList();

        var result = GlV2Formula.Rate(
            Rates.Value, inputs.State, classes,
            inputs.OccLimit, inputs.PcoAggregate, inputs.ScheduleModifier,
            inputs.IncludeTria, inputs.LoggingLumberingLimit);

        Assert.True(Money(result.ManualPremium) == expected.ManualPremium,
            $"[{fixtureName}] ManualPremium: expected {expected.ManualPremium}, got {Money(result.ManualPremium)}");
        Assert.True(result.ModifiedPremium == expected.ModifiedPremium,
            $"[{fixtureName}] ModifiedPremium: expected {expected.ModifiedPremium}, got {result.ModifiedPremium}");
        Assert.True(Money(result.EndorsementTotal) == expected.EndorsementTotal,
            $"[{fixtureName}] EndorsementTotal: expected {expected.EndorsementTotal}, got {Money(result.EndorsementTotal)}");
        Assert.True(result.TriaPremium == expected.TriaPremium,
            $"[{fixtureName}] TriaPremium: expected {expected.TriaPremium}, got {result.TriaPremium}");
        Assert.True(Money(result.GrandTotal) == expected.GrandTotal,
            $"[{fixtureName}] GrandTotal: expected {expected.GrandTotal}, got {Money(result.GrandTotal)}");
        Assert.True(result.Lines.Count == expected.Lines.Count,
            $"[{fixtureName}] Line count: expected {expected.Lines.Count}, got {result.Lines.Count}");

        for (int i = 0; i < expected.Lines.Count; i++)
        {
            Assert.True(result.Lines[i].ClassCode == expected.Lines[i].ClassCode,
                $"[{fixtureName}] Line {i + 1} ClassCode: expected {expected.Lines[i].ClassCode}, got {result.Lines[i].ClassCode}");
            Assert.True(Money(result.Lines[i].LineTotal) == expected.Lines[i].LinePremium,
                $"[{fixtureName}] Line {i + 1} LinePremium: expected {expected.Lines[i].LinePremium}, got {Money(result.Lines[i].LineTotal)}");
        }
    }

    [Fact]
    public void GL_v2_unfiled_class_state_refers_to_company()
    {
        // 49451 (Vacant Land) is "(a)" — no filed rate — in TX.
        var ex = Assert.Throws<InvalidOperationException>(() => GlV2Formula.Rate(
            Rates.Value, "TX",
            [new GlV2Formula.ClassInput("49451", 100)],
            1_000_000, 2_000_000, 1.0m,
            includeTria: false));
        Assert.Contains("refer to company", ex.Message);
    }

    private static GlV2Formula.RateData LoadRateData()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "GL_v2", "rate_data.json");
        var doc = JsonSerializer.Deserialize<RateDataFile>(File.ReadAllText(path), JsonOpts)!;

        var classes = doc.Classes.ToDictionary(
            c => c.Code,
            c => new GlV2Formula.ClassConfig(c.Code, c.Desc, c.Basis, c.HasPco, c.PoTier, c.PcoTier, c.Divisor));

        var lc334 = new Dictionary<(string, string), decimal>();
        foreach (var (code, byState) in doc.Lc334)
            foreach (var (st, v) in byState)
                lc334[(code, st)] = v;

        var lc336 = new Dictionary<(string, string), decimal>();
        foreach (var (code, byState) in doc.Lc336)
            foreach (var (st, v) in byState)
                lc336[(code, st)] = v;

        var ilf = new Dictionary<(int, string), decimal>();
        foreach (var row in doc.Ilf)
        {
            void Put(string tier, decimal? v) { if (v.HasValue) ilf[(row.Limit, tier)] = v.Value; }
            Put("PO_T1", row.PO_T1); Put("PO_T2", row.PO_T2); Put("PO_T3", row.PO_T3);
            Put("PCO_TA", row.PCO_TA); Put("PCO_TB", row.PCO_TB); Put("PCO_TC", row.PCO_TC);
        }

        var ll = doc.LlEndorsement.ToDictionary(r => r.Limit, r => (r.Min, r.Pct));

        var p = doc.Params;
        return new GlV2Formula.RateData(
            p.Lcm, p.Tria, classes, lc334, lc336, ilf, ll);
    }

    // ─── fixture DTOs ────────────────────────────────────────────────────────
    private record FixtureInputs(
        string State, int OccLimit, int PcoAggregate, decimal ScheduleModifier,
        bool IncludeTria, decimal? LoggingLumberingLimit, List<FixtureLine> Lines);

    private record FixtureLine(string ClassCode, decimal Exposure);

    private record FixtureExpected(
        decimal ManualPremium, decimal ModifiedPremium, decimal EndorsementTotal,
        decimal TriaPremium, decimal GrandTotal, List<FixtureExpectedLine> Lines);

    private record FixtureExpectedLine(string ClassCode, decimal LinePremium);

    // ─── rate_data.json DTOs ─────────────────────────────────────────────────
    private record RateDataFile(
        ParamsDto Params, List<ClassDto> Classes, List<IlfRow> Ilf,
        Dictionary<string, Dictionary<string, decimal>> Lc334,
        Dictionary<string, Dictionary<string, decimal>> Lc336,
        [property: JsonPropertyName("ll_endorsement")] List<LlRow> LlEndorsement);

    private record LlRow(int Limit, decimal Min, decimal Pct);

    private record ParamsDto(decimal Lcm, decimal Tria);

    private record ClassDto(
        string Code, string Desc, string Basis,
        [property: JsonPropertyName("has_pco")] bool HasPco,
        [property: JsonPropertyName("po_tier")] string PoTier,
        [property: JsonPropertyName("pco_tier")] string PcoTier,
        int Divisor);

    private record IlfRow(
        int Limit,
        decimal? PO_T1, decimal? PO_T2, decimal? PO_T3,
        decimal? PCO_TA, decimal? PCO_TB, decimal? PCO_TC);
}
