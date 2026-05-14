using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyPackageConfiguration : BaseEntity
{
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string State { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Carrier Carrier { get; set; } = null!;
    public ICollection<PolicyPackageForm> Forms { get; set; } = new List<PolicyPackageForm>();
}
