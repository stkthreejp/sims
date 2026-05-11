namespace SIMS.Domain.Entities;

public class SubmissionGLCoverages : BaseEntity
{
    public Guid SubmissionId { get; set; }
    public decimal? GeneralAggregate { get; set; }
    public decimal? ProductsCompletedOps { get; set; }
    public decimal? EachOccurrence { get; set; }
    public decimal? PersonalAndAdvInjury { get; set; }
    public decimal? DamageToRentedPremises { get; set; }
    public decimal? MedicalExpense { get; set; }
    public decimal? TotalSubcontractorCost { get; set; }

    // GL rating inputs — endorsements & surcharges
    public int AiIndividualCount { get; set; }        // # of individual AI endorsements × $50
    public bool AiBlanket { get; set; }               // blanket AI endorsement — $250 flat
    public int WosIndividualCount { get; set; }       // # of individual WOS endorsements × $50
    public bool WosBlanket { get; set; }              // blanket WOS endorsement — $250 flat
    public bool PrimaryNonContributory { get; set; }  // PNC endorsement — $250 flat
    public bool IncludeTria { get; set; }             // TRIA surcharge — 2.5% of GL premium

    public Submission Submission { get; set; } = null!;
}
