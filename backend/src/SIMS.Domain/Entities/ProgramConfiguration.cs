namespace SIMS.Domain.Entities;

public class ProgramConfiguration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<ProgramCarrier> ProgramCarriers { get; set; } = new List<ProgramCarrier>();
    public ICollection<UnderwritingGuidelineDocument> GuidelineDocuments { get; set; } = new List<UnderwritingGuidelineDocument>();
    public ICollection<UnderwritingGuidelineControl> GuidelineControls { get; set; } = new List<UnderwritingGuidelineControl>();
}
