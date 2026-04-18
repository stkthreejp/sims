namespace IMS.Domain.Entities;

public class Agent : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? AgencyName { get; set; }
    public string? LicenseNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public ICollection<AgentLocation> Locations { get; set; } = new List<AgentLocation>();
}
