using SIMS.Domain.Entities.Accounting;
using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class IntermediaryProgramCarrierLobSetup : BaseEntity
{
    public Guid IntermediaryId { get; set; }
    public Guid ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness? LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public decimal? BrokerageRate { get; set; }
    public bool CreatePayable { get; set; }
    public long? PayablePayeeId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Intermediary Intermediary { get; set; } = null!;
    public ProgramConfiguration ProgramConfiguration { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
    public Payee? PayablePayee { get; set; }
}
