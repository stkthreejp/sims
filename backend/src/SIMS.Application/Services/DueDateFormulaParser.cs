using System.Text.RegularExpressions;
using SIMS.Application.Common;

namespace SIMS.Application.Services;

public record ParsedFormula(string VarName, int Sign, int Amount, bool BusinessDays);

public static class DueDateFormulaParser
{
    // Matches: [VarName.Nested] +/- 45d  or  [VarName] - 10bd
    private static readonly Regex FormulaRegex = new(
        @"^\[([A-Za-z0-9_.]+)\]\s*([+-])\s*(\d+)(bd|d)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static Result<ParsedFormula> Parse(string? formula)
    {
        var trimmed = formula?.Trim() ?? string.Empty;
        var match = FormulaRegex.Match(trimmed);

        if (!match.Success)
            return Result<ParsedFormula>.Failure(
                "INVALID_FORMULA",
                $"Formula '{formula}' is not valid. Expected: [VarName] ± Nd or [VarName] ± Nbd");

        var varName = match.Groups[1].Value;
        var sign = match.Groups[2].Value == "+" ? 1 : -1;
        var amount = int.Parse(match.Groups[3].Value);
        var businessDays = match.Groups[4].Value.Equals("bd", StringComparison.OrdinalIgnoreCase);

        return Result<ParsedFormula>.Success(new ParsedFormula(varName, sign, amount, businessDays));
    }

    /// <summary>
    /// Steps |days| business days from start in the direction of sign(days),
    /// skipping Sat/Sun and any date in holidays.
    /// </summary>
    public static DateTime AddBusinessDays(DateTime start, int days, IEnumerable<DateOnly> holidays)
    {
        if (days == 0) return start;

        var holidaySet = new HashSet<DateOnly>(holidays);
        var direction = days > 0 ? 1 : -1;
        var remaining = Math.Abs(days);
        var current = start;

        while (remaining > 0)
        {
            current = current.AddDays(direction);
            if (current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (holidaySet.Contains(DateOnly.FromDateTime(current))) continue;
            remaining--;
        }

        return current;
    }
}
