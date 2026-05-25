using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities.Bordereaux;

public class BordereauxRun : BaseEntity
{
    public Guid BordereauxProfileId { get; set; }
    public int RunNumber { get; set; } = 1;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public BordereauxRunStatus Status { get; set; } = BordereauxRunStatus.Draft;
    public BordereauxReconciliationStatus ReconciliationStatus { get; set; } = BordereauxReconciliationStatus.NotRun;
    public Guid? GeneratedById { get; set; }
    public DateTime? GeneratedAt { get; set; }
    public string? LondonBordereauxBlobPath { get; set; }
    public string? LondonBordereauxFileName { get; set; }
    public string? LondonBordereauxContentType { get; set; }
    public string? AccountCurrentBlobPath { get; set; }
    public string? AccountCurrentFileName { get; set; }
    public string? AccountCurrentContentType { get; set; }
    public int BordereauxRowCount { get; set; }
    public int AccountCurrentRowCount { get; set; }
    public string DetailRowCountsJson { get; set; } = "{}";
    public string ValidationSummaryJson { get; set; } = "{}";
    public string ReconciliationSummaryJson { get; set; } = "{}";
    public string ProfileSnapshotJson { get; set; } = "{}";
    public string SourceRowsSnapshotJson { get; set; } = "[]";

    public BordereauxProfile Profile { get; set; } = null!;
    public User? GeneratedBy { get; set; }
}
