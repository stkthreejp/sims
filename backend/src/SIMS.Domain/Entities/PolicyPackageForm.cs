using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyPackageForm : BaseEntity
{
    public Guid PolicyPackageConfigurationId { get; set; }
    public Guid PolicyFormTemplateId { get; set; }
    public int SequenceOrder { get; set; }
    public PolicyFormType FormType { get; set; } = PolicyFormType.Mandatory;
    public string? TriggerConditionJson { get; set; }
    public string? Notes { get; set; }

    public PolicyPackageConfiguration PolicyPackageConfiguration { get; set; } = null!;
    public PolicyFormTemplate PolicyFormTemplate { get; set; } = null!;
}
