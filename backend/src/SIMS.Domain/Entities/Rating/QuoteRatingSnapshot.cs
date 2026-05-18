namespace SIMS.Domain.Entities.Rating;

public class QuoteRatingSnapshot : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Guid? PolicyTransactionId { get; set; }
    public Guid RatingPlanVersionId { get; set; }
    public DateTime RatedAt { get; set; }
    public Guid RatedById { get; set; }

    // Per-line premiums already include schedule modifier (applied inside ROUND per Excel)
    public decimal ManualPremium { get; set; }
    public decimal ScheduleModifier { get; set; } = 1.0m;
    public string? ScheduleModifierReason { get; set; }

    // Policy-level endorsements
    public bool NewlyAcquiredEquipment { get; set; }
    public bool DebrisRemoval { get; set; }
    public bool RentalReimbursement { get; set; }
    public bool TowingStorageRecovery { get; set; }
    public bool Tria { get; set; }
    public decimal EndorsementPremium { get; set; }

    public decimal GrandTotalPremium { get; set; }
    public bool IsBoundSnapshot { get; set; }

    public Quote Quote { get; set; } = null!;
    public PolicyTransaction? PolicyTransaction { get; set; }
    public RatingPlanVersion RatingPlanVersion { get; set; } = null!;
    public User RatedBy { get; set; } = null!;
    public ICollection<QuoteRatingLine> Lines { get; set; } = new List<QuoteRatingLine>();
}
