namespace SIMS.Domain.Entities.Rating;

public class RatingSettings : BaseEntity
{
    public bool ShadowModeGL { get; set; }
    public bool ShadowModeIM { get; set; }
    public bool ShadowModeAL { get; set; }
    public bool ShadowModeAPD { get; set; }
}
