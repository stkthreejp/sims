namespace SIMS.Domain.Entities;

using SIMS.Domain.Enums;

public class QuoteChecklistItem : BaseEntity
{
    public Guid QuoteId { get; set; }
    public UnderwritingControlStage Stage { get; set; } = UnderwritingControlStage.Bind;
    public string TriggerKey { get; set; } = string.Empty;   // e.g. "rated", "has_application"
    public string Label { get; set; } = string.Empty;
    public bool IsBlocker { get; set; } = true;
    public int SortOrder { get; set; }

    public bool IsCompleted { get; set; } = false;
    // "Manual" | "System"
    public string CompletionSource { get; set; } = "Manual";
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public DateTime? CompletedAt { get; set; }

    public Quote Quote { get; set; } = null!;
    public User? CompletedBy { get; set; }
}
