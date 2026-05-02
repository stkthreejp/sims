namespace SIMS.Application.DTOs.Submissions;

public class SubmissionGLCoveragesDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public decimal? GeneralAggregate { get; set; }
    public decimal? ProductsCompletedOps { get; set; }
    public decimal? EachOccurrence { get; set; }
    public decimal? PersonalAndAdvInjury { get; set; }
    public decimal? DamageToRentedPremises { get; set; }
    public decimal? MedicalExpense { get; set; }
    public decimal? TotalSubcontractorCost { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class SubmissionGLCoveragesUpsertDto
{
    public decimal? GeneralAggregate { get; set; }
    public decimal? ProductsCompletedOps { get; set; }
    public decimal? EachOccurrence { get; set; }
    public decimal? PersonalAndAdvInjury { get; set; }
    public decimal? DamageToRentedPremises { get; set; }
    public decimal? MedicalExpense { get; set; }
    public decimal? TotalSubcontractorCost { get; set; }
}

public class SubmissionGLClassificationDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public int LocationNumber { get; set; }
    public string? ClassCode { get; set; }
    public string? Description { get; set; }
    public string? PremiumBasis { get; set; }
    public decimal? Exposure { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SubmissionGLClassificationCreateDto
{
    public int LocationNumber { get; set; }
    public string? ClassCode { get; set; }
    public string? Description { get; set; }
    public string? PremiumBasis { get; set; }
    public decimal? Exposure { get; set; }
}

public class SubmissionGLClassificationUpdateDto : SubmissionGLClassificationCreateDto { }
