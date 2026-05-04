using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
using SIMS.Application.Rating;
using SIMS.Application.Rating.Tests.Helpers;

namespace SIMS.Application.Rating.Tests;

/// <summary>
/// Fixture-driven parity tests for the IM v1 rating engine.
/// Each fixture in Fixtures/IM_v1/ has inputs.json + expected.json.
/// Any fixture failure blocks merge — this is the Excel parity CI gate.
/// </summary>
public class RatingFixtureTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    public static IEnumerable<object[]> FixtureDirectories()
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "IM_v1");
        if (!Directory.Exists(baseDir))
            yield break;

        foreach (var dir in Directory.GetDirectories(baseDir).OrderBy(d => d))
            yield return [Path.GetFileName(dir), dir];
    }

    [Theory]
    [MemberData(nameof(FixtureDirectories))]
    public void IM_v1_fixture_matches(string fixtureName, string fixtureDir)
    {
        var inputsJson = File.ReadAllText(Path.Combine(fixtureDir, "inputs.json"));
        var expectedJson = File.ReadAllText(Path.Combine(fixtureDir, "expected.json"));

        var inputs = JsonSerializer.Deserialize<FixtureInputs>(inputsJson, JsonOpts)!;
        var expected = JsonSerializer.Deserialize<FixtureExpected>(expectedJson, JsonOpts)!;

        var items = inputs.Items.Select(i =>
            new ImV1Formula.EquipmentInput(i.TypeNumber, i.Year, i.Value, i.Deductible)).ToList();

        var result = ImV1Formula.Rate(
            ImV1FactorTables.BaseRate,
            ImV1FactorTables.DeductibleFactor,
            items,
            inputs.EffectiveYear,
            inputs.ScheduleModifier,
            inputs.MinimumPremium);

        Assert.True(result.ManualPremium == expected.ManualPremium,
            $"[{fixtureName}] ManualPremium: expected {expected.ManualPremium}, got {result.ManualPremium}");
        Assert.True(result.GrandTotal == expected.GrandTotal,
            $"[{fixtureName}] GrandTotal: expected {expected.GrandTotal}, got {result.GrandTotal}");
        Assert.True(result.Lines.Count == expected.Lines.Count,
            $"[{fixtureName}] Line count: expected {expected.Lines.Count}, got {result.Lines.Count}");

        for (int i = 0; i < expected.Lines.Count; i++)
        {
            var expLine = expected.Lines[i];
            var actLine = result.Lines[i];

            Assert.True(actLine.LinePremium == expLine.LinePremium,
                $"[{fixtureName}] Line {i + 1} LinePremium: expected {expLine.LinePremium}, got {actLine.LinePremium}");
            Assert.True(actLine.AgeBand == expLine.AgeBand,
                $"[{fixtureName}] Line {i + 1} AgeBand: expected '{expLine.AgeBand}', got '{actLine.AgeBand}'");
            Assert.True(actLine.DeductibleKey == expLine.DeductibleKey,
                $"[{fixtureName}] Line {i + 1} DeductibleKey: expected '{expLine.DeductibleKey}', got '{actLine.DeductibleKey}'");
            Assert.True(actLine.BaseRate == expLine.BaseRate,
                $"[{fixtureName}] Line {i + 1} BaseRate: expected {expLine.BaseRate}, got {actLine.BaseRate}");
            Assert.True(actLine.DeductibleFactor == expLine.DeductibleFactor,
                $"[{fixtureName}] Line {i + 1} DeductibleFactor: expected {expLine.DeductibleFactor}, got {actLine.DeductibleFactor}");
        }
    }

    // ─── DTOs for JSON deserialization ───────────────────────────────────────

    private record FixtureInputs(
        int EffectiveYear,
        decimal ScheduleModifier,
        decimal? MinimumPremium,
        List<FixtureItem> Items
    );

    private record FixtureItem(
        int TypeNumber,
        int? Year,
        decimal Value,
        decimal? Deductible
    );

    private record FixtureExpected(
        decimal ManualPremium,
        decimal GrandTotal,
        List<FixtureExpectedLine> Lines
    );

    private record FixtureExpectedLine(
        decimal LinePremium,
        string AgeBand,
        string DeductibleKey,
        decimal BaseRate,
        decimal DeductibleFactor
    );
}
