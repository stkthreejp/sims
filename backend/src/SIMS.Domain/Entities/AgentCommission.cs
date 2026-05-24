namespace SIMS.Domain.Entities;

public class AgentCommission
{
    public long Id { get; set; }
    public Guid? ProgramConfigurationId { get; set; }
    public Guid AgentId { get; set; }
    public string? LineOfBusiness { get; set; }
    public decimal CommissionRate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? DisabledDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Agent Agent { get; set; } = null!;
}
