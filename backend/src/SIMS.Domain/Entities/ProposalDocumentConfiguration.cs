using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class ProposalDocumentConfiguration : BaseEntity
{
    public Guid? ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string? State { get; set; }
    public ProposalDocumentRole Role { get; set; }
    public Guid DocumentTemplateId { get; set; }
    public int SequenceOrder { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? Notes { get; set; }

    public ProgramConfiguration? ProgramConfiguration { get; set; }
    public Carrier Carrier { get; set; } = null!;
    public DocumentTemplate DocumentTemplate { get; set; } = null!;
}
