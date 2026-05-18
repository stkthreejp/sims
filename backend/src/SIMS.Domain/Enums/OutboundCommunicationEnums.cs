namespace SIMS.Domain.Enums;

public enum OutboundCommunicationEntityType
{
    Submission = 0,
    Quote = 1,
    Policy = 2,
    Carrier = 3,
    Agent = 4,
    Insured = 5,
}

public enum OutboundCommunicationStatus
{
    Draft = 0,
    Queued = 1,
    Sent = 2,
    Failed = 3,
    Cancelled = 4,
}

public enum OutboundCommunicationPurpose
{
    Proposal = 0,
    Binder = 1,
    PolicyIssue = 2,
    Endorsement = 3,
    CancellationNotice = 4,
    NonRenewalNotice = 5,
    RenewalInvitation = 6,
    InternalReferral = 7,
    CarrierReporting = 8,
    Other = 9,
}

public enum OutboundCommunicationSenderType
{
    CurrentUser = 0,
    SharedMailbox = 1,
    System = 2,
}
