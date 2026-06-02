using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyPackageConfiguration : BaseEntity
{
    public Guid? ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string? State { get; set; }
    public Guid? ProgramCarrierLineOfBusinessId { get; set; }
    public Guid? ProgramCarrierLobStateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Carrier Carrier { get; set; } = null!;
    public ProgramCarrierLineOfBusiness? ProgramCarrierLineOfBusiness { get; set; }
    public ProgramCarrierLobState? ProgramCarrierLobState { get; set; }
    public ICollection<PolicyPackageForm> Forms { get; set; } = new List<PolicyPackageForm>();
}
