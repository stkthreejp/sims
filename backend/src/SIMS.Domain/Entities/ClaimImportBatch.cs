namespace SIMS.Domain.Entities;

public class ClaimImportBatch : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? TpaName { get; set; }
    public DateOnly ValuationDate { get; set; }

    public int RecordCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }

    // "Pending" | "Processing" | "Complete" | "Failed"
    public string Status { get; set; } = "Pending";
    public string? ErrorSummaryJson { get; set; }

    public Guid ImportedById { get; set; }

    // Navigation
    public User ImportedBy { get; set; } = null!;
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}
