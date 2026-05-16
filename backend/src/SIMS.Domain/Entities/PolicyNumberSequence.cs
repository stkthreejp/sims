using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class PolicyNumberSequence : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = "POL-{YYYY}-{SEQ:00000}";
    public long NextNumber { get; set; } = 1;
    public bool ResetAnnually { get; set; }
    public int? LastResetYear { get; set; }
    public string TermSuffixFormat { get; set; } = "-{TERM:00}";
    public PolicyNumberRenewalBehavior RenewalBehavior { get; set; } = PolicyNumberRenewalBehavior.CopyBaseAndIncrementTermSuffix;
    public bool AllowManualOverride { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    public ICollection<PolicyNumberAssignment> Assignments { get; set; } = new List<PolicyNumberAssignment>();
    public ICollection<PolicyNumberSequenceUsage> Usages { get; set; } = new List<PolicyNumberSequenceUsage>();
}
