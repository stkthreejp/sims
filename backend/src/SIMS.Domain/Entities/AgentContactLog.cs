namespace SIMS.Domain.Entities;

public enum AgentContactLogType
{
    Visit,
    Call,
    Email,
    Other,
}

public class AgentContactLog : BaseEntity
{
    public Guid AgentId { get; set; }
    public DateOnly LogDate { get; set; }
    public AgentContactLogType LogType { get; set; }
    public string? ContactName { get; set; }
    public string Notes { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }

    public Agent Agent { get; set; } = null!;
}
