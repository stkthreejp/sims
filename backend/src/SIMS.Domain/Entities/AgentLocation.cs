namespace SIMS.Domain.Entities;

public class AgentLocation : BaseEntity
{
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;

    public string? Name { get; set; }          // e.g. "Main Office", "Downtown Branch"
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }

    public ICollection<AgentContact> Contacts { get; set; } = new List<AgentContact>();
}
