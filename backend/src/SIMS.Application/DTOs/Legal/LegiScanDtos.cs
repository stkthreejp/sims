namespace SIMS.Application.DTOs.Legal;

public sealed record LegiScanStatusDto(
    bool IsConfigured,
    int MaxMonitoredBills,
    int MonthlyQueryLimit,
    int LocalTrackedBillCount);

public sealed record LegiScanTrackedBillDto(
    Guid Id,
    int BillId,
    string State,
    string BillNumber,
    string Title,
    string? ChangeHash,
    int? Status,
    DateOnly? StatusDate,
    string? Url,
    string? Stance,
    bool IsActive,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record LegiScanMonitorRequest(int[] BillIds, string? Stance);

public sealed record LegiScanSyncResultDto(
    Guid ScanRunId,
    int RemoteMonitorCount,
    int AddedOrUpdatedCount,
    int ChangedBillCount,
    int QueryBudgetUsed,
    string[] Warnings);
