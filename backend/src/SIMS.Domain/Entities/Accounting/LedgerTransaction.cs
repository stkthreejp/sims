namespace SIMS.Domain.Entities.Accounting;

public class LedgerTransaction
{
    public long Id { get; set; }
    public int TenantId { get; set; } = 1;
    public Guid TransactionId { get; set; }       // groups the debit+credit pair(s)
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
    public DateOnly EffectiveDate { get; set; }
    public int AccountId { get; set; }
    public decimal Debit { get; set; } = 0;
    public decimal Credit { get; set; } = 0;
    public string SourceType { get; set; } = string.Empty;  // 'Invoice'|'Receipt'|'Disbursement'|'Adjustment'
    public long SourceId { get; set; }
    public string? Memo { get; set; }
    public Guid CreatedBy { get; set; }
    public long? RolledUpIn { get; set; }

    // Void/reversal tracking
    public string PostingStatus { get; set; } = "Posted";  // 'Posted'|'Voided'|'Reversal'
    public Guid? VoidedByTransactionId { get; set; }
    public Guid? ReversesTransactionId { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }
    public string? VoidReason { get; set; }

    public LedgerAccount Account { get; set; } = null!;
    public JournalEntryRollup? Rollup { get; set; }
}
