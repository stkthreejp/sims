using IMS.Application.Common;

namespace IMS.Application.Interfaces.Services;

public interface IDueDateFormulaService
{
    Task<Result<DateTime>> EvaluateAsync(string formula, Dictionary<string, DateTime> context);
}
