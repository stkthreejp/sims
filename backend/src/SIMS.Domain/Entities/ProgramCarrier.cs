namespace SIMS.Domain.Entities;

public class ProgramCarrier : BaseEntity
{
    public Guid ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }

    public ProgramConfiguration ProgramConfiguration { get; set; } = null!;
    public Carrier Carrier { get; set; } = null!;
    public ICollection<ProgramCarrierLineOfBusiness> LinesOfBusiness { get; set; } = new List<ProgramCarrierLineOfBusiness>();
}
