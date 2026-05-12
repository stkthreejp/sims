using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Carriers;

public class CarrierAdditionalInterestRateDto
{
    public Guid Id { get; set; }
    public Guid? CarrierId { get; set; }
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
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CarrierAdditionalInterestRateCreateDto
{
    public Guid? CarrierId { get; set; }
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

public class CarrierAdditionalInterestRateUpdateDto : CarrierAdditionalInterestRateCreateDto { }
