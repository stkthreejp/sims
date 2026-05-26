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
    string? LondonBordereauxBlobPath,
    string? LondonBordereauxFileName,
    string? LondonBordereauxContentType,
    string? AccountCurrentBlobPath,
    string? AccountCurrentFileName,
    string? AccountCurrentContentType,
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

public record ReconcileBordereauxRunRequest(
    int AccountCurrentRowCount,
    decimal AccountCurrentGrossPremiumTotal,
    decimal AccountCurrentGrossCommissionTotal,
    decimal AccountCurrentFeesTotal,
    decimal AccountCurrentNetDueCarrierTotal);

public enum BordereauxRunFileKind
{
    LondonBordereaux = 1,
    AccountCurrent = 2,
}
