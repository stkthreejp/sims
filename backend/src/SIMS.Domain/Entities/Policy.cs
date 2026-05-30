using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Policy : BaseEntity
{
    public string PolicyNumber { get; set; } = string.Empty;
    public string? BasePolicyNumber { get; set; }
    public int PolicyTermNumber { get; set; } = 1;
    public Guid? PolicyNumberSequenceId { get; set; }
    public Guid? PolicyNumberAssignmentId { get; set; }
    public Guid? WritingCompanyId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid BoundQuoteId { get; set; }
    public Guid? ProgramId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public PolicyStatus Status { get; set; } = PolicyStatus.Active;
    public DateOnly BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }
    public DateOnly? NonRenewedDate { get; set; }

    // Navigation
    public Submission Submission { get; set; } = null!;
    public Quote BoundQuote { get; set; } = null!;
    public ProgramConfiguration? Program { get; set; }
    public Carrier Carrier { get; set; } = null!;
    public ICollection<PolicyTransaction> Transactions { get; set; } = new List<PolicyTransaction>();
    public ICollection<PolicyVersion> Versions { get; set; } = new List<PolicyVersion>();
    public ICollection<Submission> RenewalSubmissions { get; set; } = new List<Submission>();
}
