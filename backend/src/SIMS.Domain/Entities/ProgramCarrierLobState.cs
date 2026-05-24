namespace SIMS.Domain.Entities;

public class ProgramCarrierLobState : BaseEntity
{
    public Guid ProgramCarrierLineOfBusinessId { get; set; }
    public string StateCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }

    public ProgramCarrierLineOfBusiness ProgramCarrierLineOfBusiness { get; set; } = null!;
}
