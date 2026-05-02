using IMS.Application.Interfaces.Services;
using Xunit;
using IMS.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Tests.Services;

/// <summary>
/// Tests the service orchestration layer for paths that don't require the database
/// (calendar-day formulas and early-exit error paths).
/// Business-day DB integration is covered by DueDateFormulaParserTests.
/// </summary>
public class DueDateFormulaServiceTests
{
    private static IDueDateFormulaService CreateService()
    {
        // Minimal IServiceProvider — returns null for DbContext.
        // Safe for calendar-day and error paths that never reach the DB.
        var sp = new NullDbContextServiceProvider();
        return new DueDateFormulaService(sp);
    }

    [Fact]
    public async Task EvaluateAsync_CalendarDayAdd_ReturnsCorrectDate()
    {
        var svc = CreateService();
        var baseDate = new DateTime(2024, 3, 1);
        var context = new Dictionary<string, DateTime> { ["Policy.EffectiveDate"] = baseDate };

        var result = await svc.EvaluateAsync("[Policy.EffectiveDate] + 30d", context);

        Assert.True(result.IsSuccess);
        Assert.Equal(baseDate.AddDays(30), result.Value);
    }

    [Fact]
    public async Task EvaluateAsync_CalendarDaySubtract_ReturnsCorrectDate()
    {
        var svc = CreateService();
        var baseDate = new DateTime(2024, 3, 15);
        var context = new Dictionary<string, DateTime> { ["Policy.EffectiveDate"] = baseDate };

        var result = await svc.EvaluateAsync("[Policy.EffectiveDate] - 45d", context);

        Assert.True(result.IsSuccess);
        Assert.Equal(baseDate.AddDays(-45), result.Value);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidFormula_ReturnsFailure()
    {
        var svc = CreateService();

        var result = await svc.EvaluateAsync("Policy.EffectiveDate - 45d", []);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FORMULA", result.ErrorCode);
    }

    [Fact]
    public async Task EvaluateAsync_UnknownVariable_ReturnsFailure()
    {
        var svc = CreateService();

        var result = await svc.EvaluateAsync("[Policy.EffectiveDate] + 5d", []);

        Assert.False(result.IsSuccess);
        Assert.Equal("UNKNOWN_VARIABLE", result.ErrorCode);
    }

    // ── Minimal stub ──────────────────────────────────────────────────────

    private sealed class NullDbContextServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(DbContext) ? null : null;
    }
}
