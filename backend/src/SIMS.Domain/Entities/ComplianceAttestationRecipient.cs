namespace SIMS.Domain.Entities;

public class ComplianceAttestationRecipient : BaseEntity
{
    public Guid CampaignId { get; set; }
    public ComplianceAttestationCampaign Campaign { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public DateTime? AttestedAt { get; set; }
    public string? Comment { get; set; }
}
