using SIMS.Application.DTOs.Accounting;
using SIMS.Domain.Entities.Accounting;

namespace SIMS.Application.Interfaces.Services;

public interface IPeriodCloseService
{
    Task<IReadOnlyList<AccountingPeriodDto>> GetPeriodsAsync(CancellationToken ct = default);
    Task<AccountingPeriodDto> GetOrCreatePeriodAsync(int year, int month, CancellationToken ct = default);
    Task<AccountingPeriodDto> EvaluateChecklistAsync(long periodId, CancellationToken ct = default);
    Task<PeriodCloseResultDto> ClosePeriodAsync(long periodId, string? notes, Guid userId, CancellationToken ct = default);
    Task<PeriodCloseResultDto> ReopenPeriodAsync(long periodId, string? reason, Guid userId, CancellationToken ct = default);

    /// <summary>Returns the current open period, or null if none exists.</summary>
    Task<AccountingPeriod?> GetCurrentOpenPeriodAsync(CancellationToken ct = default);

    /// <summary>Returns the status of the period containing the given date.</summary>
    Task<string?> GetPeriodStatusForDateAsync(DateOnly date, CancellationToken ct = default);
}
