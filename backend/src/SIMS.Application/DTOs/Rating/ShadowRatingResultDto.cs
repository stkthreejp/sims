using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Rating;

public class ShadowSettingsDto
{
    public bool GL { get; set; }
    public bool IM { get; set; }
    public bool AL { get; set; }
    public bool APD { get; set; }

    public bool IsEnabledFor(PolicyLineOfBusiness lob) => lob switch
    {
        PolicyLineOfBusiness.GeneralLiability => GL,
        PolicyLineOfBusiness.InlandMarine => IM,
        PolicyLineOfBusiness.AutoLiability => AL,
        PolicyLineOfBusiness.AutoPhysicalDamage => APD,
        _ => false,
    };
}


public class ShadowRatingResultDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public string QuoteNumber { get; set; } = "";
    public string InsuredName { get; set; } = "";
    public Guid RatingPlanVersionId { get; set; }
    public string PlanName { get; set; } = "";
    public int VersionNumber { get; set; }
    public DateTime RatedAt { get; set; }
    public Guid RatedById { get; set; }
    public string RatedByName { get; set; } = "";
    public decimal ShadowPremium { get; set; }
    public decimal ActualPremium { get; set; }
    public decimal DeltaAmount { get; set; }
    public decimal DeltaPct { get; set; }
    public bool IsOutlier { get; set; }
    public decimal ScheduleModifier { get; set; }
}
