namespace SIMS.Domain.Entities;

public class SystemEvent : BaseEntity
{
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
}
