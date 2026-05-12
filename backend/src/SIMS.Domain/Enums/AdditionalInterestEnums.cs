namespace SIMS.Domain.Enums;

public enum AdditionalInterestAppliesToType
{
    Blanket = 0,
    ScheduledItems = 1,
}

public enum AdditionalInterestChargeMethod
{
    NoCharge = 0,
    Included = 1,
    PerInterest = 2,
    BlanketFlat = 3,
}

public enum AdditionalInterestCoverageType
{
    AdditionalInsured = 0,
    LossPayee = 1,
    WaiverOfSubrogation = 2,
    PrimaryNonContributory = 3,
}
