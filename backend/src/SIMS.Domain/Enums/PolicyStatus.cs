namespace SIMS.Domain.Enums;

public enum PolicyStatus
{
    Active = 1,
    Renewed = 2,      // superseded by a new-term policy
    NonRenewed = 3,   // deliberate non-renewal with legal notice
    Expired = 4,      // reached expiration with no action
    Cancelled = 5
}
