using SIMS.Application.Common;

namespace SIMS.Application.Interfaces.Services;

public interface IDueDateFormulaService
{
    Task<Result<DateTime>> EvaluateAsync(string formula, Dictionary<string, DateTime> context);
}
