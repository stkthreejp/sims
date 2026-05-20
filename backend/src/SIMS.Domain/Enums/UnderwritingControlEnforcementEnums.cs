namespace SIMS.Domain.Enums;

public enum UnderwritingControlTargetType
{
    Quote = 1,
    Policy = 2
}

public enum UnderwritingControlEvaluationStatus
{
    Passed = 1,
    Warning = 2,
    ReferralRequired = 3,
    Blocked = 4,
    NotApplicable = 5,
    UnknownField = 6,
    Overridden = 7
}
