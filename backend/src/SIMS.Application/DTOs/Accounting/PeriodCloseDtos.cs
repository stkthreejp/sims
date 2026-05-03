namespace SIMS.Application.DTOs.Accounting;

public record AccountingPeriodDto(
    long Id,
    int PeriodYear,
    int PeriodMonth,
    string Status,
    DateTime? ClosedAt,
    DateTime? ReopenedAt,
    string? Notes,
    IReadOnlyList<ChecklistItemDto> Checklist
);

public record ChecklistItemDto(
    string CheckKey,
    string Label,
    int IssueCount,
    bool IsBlocking,
    bool Passed,
    DateTime? LastCheckedAt
);

public record ClosePeriodRequest(string? Notes);

public record ReopenPeriodRequest(string? Reason);

public record PeriodCloseResultDto(
    bool Success,
    string? ErrorCode,
    string? ErrorMessage,
    AccountingPeriodDto? Period
);
