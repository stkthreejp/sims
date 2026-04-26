namespace IMS.Domain.Entities;

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

    public Submission Submission { get; set; } = null!;
}
