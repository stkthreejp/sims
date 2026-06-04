namespace SIMS.Domain.Entities;

public class AgentCommission
{
    public long Id { get; set; }
    public Guid? ProgramConfigurationId { get; set; }
    public Guid? CarrierId { get; set; }
    public Guid AgentId { get; set; }
    public string? LineOfBusiness { get; set; }
    public string? StateCode { get; set; }
    public Guid? ProgramCarrierId { get; set; }
    public Guid? ProgramCarrierLineOfBusinessId { get; set; }
    public Guid? ProgramCarrierLobStateId { get; set; }
    public decimal CommissionRate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? DisabledDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Carrier? Carrier { get; set; }
    public Agent Agent { get; set; } = null!;
    public ProgramCarrier? ProgramCarrier { get; set; }
    public ProgramCarrierLineOfBusiness? ProgramCarrierLineOfBusiness { get; set; }
    public ProgramCarrierLobState? ProgramCarrierLobState { get; set; }
}
