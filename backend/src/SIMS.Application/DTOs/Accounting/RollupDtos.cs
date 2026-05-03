namespace SIMS.Application.DTOs.Accounting;

public record RollupSummaryDto(
    long Id,
    int PeriodYear,
    int PeriodMonth,
    string DriverType,
    string Status,
    int TransactionCount,
    string? ExternalId,
    string? BlobUri,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record RollupDto(
    long Id,
    int PeriodYear,
    int PeriodMonth,
    string DriverType,
    string Status,
    int TransactionCount,
    int LineCount,
    string? ExternalId,
    string? BlobUri,
    string? ErrorMessage,
    DateTime CreatedAt,
    DateTime? CompletedAt
);

public record TriggerRollupRequest(
    int PeriodYear,
    int PeriodMonth,
    string DriverType   // 'CSV'|'QBO'
);
