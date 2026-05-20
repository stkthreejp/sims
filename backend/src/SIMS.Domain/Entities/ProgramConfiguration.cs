using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class ProgramConfiguration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string StateCode { get; set; } = "ALL";
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public Carrier? Carrier { get; set; }
    public ICollection<UnderwritingGuidelineDocument> GuidelineDocuments { get; set; } = new List<UnderwritingGuidelineDocument>();
    public ICollection<UnderwritingGuidelineControl> GuidelineControls { get; set; } = new List<UnderwritingGuidelineControl>();
}
