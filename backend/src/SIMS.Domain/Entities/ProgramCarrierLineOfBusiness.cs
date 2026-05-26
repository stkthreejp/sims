using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class ProgramCarrierLineOfBusiness : BaseEntity
{
    public Guid ProgramCarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? BillingMode { get; set; }
    public int? PaymentTermsDays { get; set; }
    public string? LondonUmr { get; set; }
    public string? LondonSectionNumber { get; set; }
    public string? LondonClassOfBusiness { get; set; }
    public string? LondonRiskCode { get; set; }
    public string? LondonInsuranceType { get; set; }
    public string? Notes { get; set; }

    public ProgramCarrier ProgramCarrier { get; set; } = null!;
    public ICollection<ProgramCarrierLobState> States { get; set; } = new List<ProgramCarrierLobState>();
}
