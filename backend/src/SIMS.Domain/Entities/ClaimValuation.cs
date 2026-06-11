using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

// One snapshot of a claim's financials at a valuation date. Batch-keyed: every
// import (and manual create/edit) upserts the row for its valuation date, so
// loss runs can be produced as-of any date regardless of feed cadence.
public class ClaimValuation : BaseEntity
{
    public Guid ClaimId { get; set; }
    public DateOnly ValuationDate { get; set; }

    public ClaimStatus Status { get; set; }
    public decimal Paid { get; set; }
    public decimal Reserved { get; set; }
    public decimal Expense { get; set; }
    public decimal Recovery { get; set; }
    public decimal Incurred { get; set; }

    public Guid? ImportBatchId { get; set; }

    // Navigation
    public Claim Claim { get; set; } = null!;
}
