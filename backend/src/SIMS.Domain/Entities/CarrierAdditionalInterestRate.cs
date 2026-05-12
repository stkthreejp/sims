using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class CarrierAdditionalInterestRate : BaseEntity
{
    public Guid? CarrierId { get; set; }
    public Carrier? Carrier { get; set; }
    public PolicyLineOfBusiness? LineOfBusiness { get; set; }
    public AdditionalInterestCoverageType CoverageType { get; set; }
    public AdditionalInterestChargeMethod ChargeMethod { get; set; }
    public decimal? PerInterestAmount { get; set; }
    public decimal? BlanketAmount { get; set; }
    public decimal? MinimumCharge { get; set; }
    public decimal? MaximumCharge { get; set; }
    public string? State { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public bool IsActive { get; set; } = true;
}
