namespace SIMS.Domain.Enums;

public enum UnderwritingControlItemType
{
    AppetiteRule = 1,
    ReferralTrigger = 2,
    AuthorityLimit = 3,
    DocumentChecklistItem = 4,
    AppetiteNote = 5
}

public enum UnderwritingControlStage
{
    Submission = 1,
    Quote = 2,
    Bind = 3,
    Issue = 4,
    PostBind = 5,
    Renewal = 6
}

public enum UnderwritingControlSeverity
{
    Informational = 1,
    Warning = 2,
    ReferralRequired = 3,
    HardBlock = 4
}

public enum UnderwritingControlStatus
{
    AiSuggested = 1,
    Draft = 2,
    Approved = 3,
    Published = 4,
    Rejected = 5,
    Retired = 6
}

