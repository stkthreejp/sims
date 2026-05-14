using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Quotes;

public class QuotePolicyFormSelectionDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public Guid PolicyFormTemplateId { get; set; }
    public string FormNumber { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public string? EditionDate { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; }
    public bool IsIncluded { get; set; }
    public bool IsSystemGenerated { get; set; }
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }
}

public class QuotePolicyFormSelectionUpsertDto
{
    public Guid PolicyFormTemplateId { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; } = PolicyFormType.Mandatory;
    public bool IsIncluded { get; set; } = true;
    public bool IsSystemGenerated { get; set; }
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }
}
