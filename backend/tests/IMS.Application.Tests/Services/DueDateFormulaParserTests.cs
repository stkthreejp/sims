using IMS.Application.Services;
using Xunit;

namespace IMS.Application.Tests.Services;

public class DueDateFormulaParserTests
{
    // ── Parse ──────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidCalendarDayFormula_Succeeds()
    {
        var result = DueDateFormulaParser.Parse("[Policy.EffectiveDate] - 45d");

        Assert.True(result.IsSuccess);
        Assert.Equal("Policy.EffectiveDate", result.Value!.VarName);
        Assert.Equal(-1, result.Value.Sign);
        Assert.Equal(45, result.Value.Amount);
        Assert.False(result.Value.BusinessDays);
    }

    [Fact]
    public void Parse_ValidBusinessDayFormula_Succeeds()
    {
        var result = DueDateFormulaParser.Parse("[Submission.ReceivedDate] + 10bd");

        Assert.True(result.IsSuccess);
        Assert.Equal("Submission.ReceivedDate", result.Value!.VarName);
        Assert.Equal(1, result.Value.Sign);
        Assert.Equal(10, result.Value.Amount);
        Assert.True(result.Value.BusinessDays);
    }

    [Theory]
    [InlineData("Policy.EffectiveDate - 45d")]   // missing brackets
    [InlineData("[Policy.EffectiveDate] 45d")]    // missing operator
    [InlineData("[Policy.EffectiveDate] + d")]    // missing number
    [InlineData("")]
    [InlineData(null)]
    public void Parse_InvalidFormula_ReturnsFailureWithMessage(string? formula)
    {
        var result = DueDateFormulaParser.Parse(formula);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FORMULA", result.ErrorCode);
        Assert.NotEmpty(result.ErrorMessage!);
    }

    // ── Calendar-day arithmetic ────────────────────────────────────────────

    [Fact]
    public void AddBusinessDays_ZeroDays_ReturnsSameDate()
    {
        var start = new DateTime(2024, 1, 15); // Monday
        Assert.Equal(start, DueDateFormulaParser.AddBusinessDays(start, 0, []));
    }

    // ── Business-day skipping: weekends ───────────────────────────────────

    [Fact]
    public void AddBusinessDays_SkipsWeekend_WhenCrossingFridayToMonday()
    {
        // Thursday Jan 11 + 2 bd → Friday Jan 12, then Mon Jan 15 (skip Sat/Sun)
        var start = new DateTime(2024, 1, 11); // Thursday

        var result = DueDateFormulaParser.AddBusinessDays(start, 2, []);

        Assert.Equal(new DateTime(2024, 1, 15), result); // Monday
    }

    [Fact]
    public void AddBusinessDays_SubtractsSkippingWeekend()
    {
        // Monday Jan 15 - 2 bd → Friday Jan 12, then Thu Jan 11 (skip Sat/Sun going back)
        var start = new DateTime(2024, 1, 15); // Monday

        var result = DueDateFormulaParser.AddBusinessDays(start, -2, []);

        Assert.Equal(new DateTime(2024, 1, 11), result); // Thursday
    }

    // ── Business-day skipping: holidays ───────────────────────────────────

    [Fact]
    public void AddBusinessDays_SkipsHoliday()
    {
        // Thursday Jan 11 + 2 bd, but Jan 12 (Friday) is a holiday
        // → skip Jan 12 (holiday), skip Jan 13-14 (weekend), land on Jan 15 and Jan 16
        var start = new DateTime(2024, 1, 11); // Thursday
        var holidays = new[] { new DateOnly(2024, 1, 12) }; // Friday is a holiday

        var result = DueDateFormulaParser.AddBusinessDays(start, 2, holidays);

        Assert.Equal(new DateTime(2024, 1, 16), result); // Tuesday (Mon + 1)
    }

    [Fact]
    public void AddBusinessDays_SkipsMultipleConsecutiveHolidays()
    {
        // Monday Jan 15 + 1 bd, but Tue Jan 16 and Wed Jan 17 are holidays → Thu Jan 18
        var start = new DateTime(2024, 1, 15); // Monday
        var holidays = new[]
        {
            new DateOnly(2024, 1, 16),
            new DateOnly(2024, 1, 17),
        };

        var result = DueDateFormulaParser.AddBusinessDays(start, 1, holidays);

        Assert.Equal(new DateTime(2024, 1, 18), result); // Thursday
    }
}
