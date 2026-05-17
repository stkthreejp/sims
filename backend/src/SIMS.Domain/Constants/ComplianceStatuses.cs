namespace SIMS.Domain.Constants;

public static class ComplianceDocumentStatus
{
    public const string Draft = "Draft";
    public const string UnderReview = "Under Review";
    public const string NeedsUpdate = "Needs Update";
    public const string Active = "Active";
    public const string Retired = "Retired";
}

public static class ComplianceVersionStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
}

public static class ComplianceReviewStatus
{
    public const string Completed = "Completed";
    public const string Approved = "Approved";
}

public static class ComplianceAttestationStatus
{
    public const string Pending = "Pending";
    public const string Attested = "Attested";
    public const string Declined = "Declined";
}

public static class ComplianceCampaignStatus
{
    public const string Active = "Active";
    public const string Closed = "Closed";
}
