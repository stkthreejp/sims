namespace IMS.Domain.Entities;

public class AgentContact : BaseEntity
{
    public Guid AgentLocationId { get; set; }
    public AgentLocation Location { get; set; } = null!;

    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}
