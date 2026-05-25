using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Bordereaux;

public record BordereauxRunDto(
    Guid Id,
    Guid BordereauxProfileId,
    string ProfileName,
    int RunNumber,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    BordereauxRunStatus Status,
    BordereauxReconciliationStatus ReconciliationStatus,
    Guid? GeneratedById,
    DateTime? GeneratedAt,
    int BordereauxRowCount,
    int AccountCurrentRowCount,
    string DetailRowCountsJson,
    string ValidationSummaryJson,
    string ReconciliationSummaryJson,
    string ProfileSnapshotJson,
    string SourceRowsSnapshotJson);

public record CreatePremiumBordereauxRunRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd);
