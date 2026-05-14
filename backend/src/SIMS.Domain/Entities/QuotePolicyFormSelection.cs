using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class QuotePolicyFormSelection : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Guid PolicyFormTemplateId { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; } = PolicyFormType.Mandatory;
    public bool IsIncluded { get; set; } = true;
    public bool IsSystemGenerated { get; set; } = true;
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }

    public Quote Quote { get; set; } = null!;
    public PolicyFormTemplate PolicyFormTemplate { get; set; } = null!;
}
