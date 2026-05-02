using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class CarrierLineOfBusiness
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }

    // Navigation
    public Carrier Carrier { get; set; } = null!;
}
