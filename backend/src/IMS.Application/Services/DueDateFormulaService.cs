using IMS.Application.Common;
using IMS.Application.Interfaces.Services;
using IMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IMS.Application.Services;

public class DueDateFormulaService : IDueDateFormulaService
{
    private readonly IServiceProvider _sp;
    private DbContext Db => (DbContext)_sp.GetService(typeof(DbContext))!;

    public DueDateFormulaService(IServiceProvider sp) => _sp = sp;

    public async Task<Result<DateTime>> EvaluateAsync(string formula, Dictionary<string, DateTime> context)
    {
        var parsed = DueDateFormulaParser.Parse(formula);
        if (!parsed.IsSuccess)
            return Result<DateTime>.Failure(parsed.ErrorCode!, parsed.ErrorMessage!);

        var (varName, sign, amount, businessDays) = parsed.Value!;

        if (!context.TryGetValue(varName, out var baseDate))
            return Result<DateTime>.Failure("UNKNOWN_VARIABLE", $"Variable '{varName}' not found in context.");

        if (!businessDays)
            return Result<DateTime>.Success(baseDate.AddDays(sign * amount));

        var holidays = await Db.Set<HolidayCalendar>()
            .Select(h => h.Date)
            .ToListAsync();

        return Result<DateTime>.Success(DueDateFormulaParser.AddBusinessDays(baseDate, sign * amount, holidays));
    }
}
