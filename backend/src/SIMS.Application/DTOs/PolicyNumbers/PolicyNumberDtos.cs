using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.PolicyNumbers;

public class PolicyNumberSequenceDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public long NextNumber { get; set; }
    public bool ResetAnnually { get; set; }
    public string TermSuffixFormat { get; set; } = string.Empty;
    public PolicyNumberRenewalBehavior RenewalBehavior { get; set; }
    public bool AllowManualOverride { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class PolicyNumberSequenceUpsertDto
{
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = "POL-{YYYY}-{SEQ:00000}";
    public long NextNumber { get; set; } = 1;
    public bool ResetAnnually { get; set; }
    public string TermSuffixFormat { get; set; } = "-{TERM:00}";
    public PolicyNumberRenewalBehavior RenewalBehavior { get; set; } = PolicyNumberRenewalBehavior.CopyBaseAndIncrementTermSuffix;
    public bool AllowManualOverride { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

public class PolicyNumberAssignmentDto
{
    public Guid Id { get; set; }
    public Guid PolicyNumberSequenceId { get; set; }
    public string SequenceName { get; set; } = string.Empty;
    public Guid? ProgramConfigurationId { get; set; }
    public string? ProgramName { get; set; }
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public Guid? WritingCompanyId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string? State { get; set; }
    public Guid? ProgramCarrierLineOfBusinessId { get; set; }
    public Guid? ProgramCarrierLobStateId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}

public class PolicyNumberAssignmentUpsertDto
{
    public Guid PolicyNumberSequenceId { get; set; }
    public Guid? ProgramConfigurationId { get; set; }
    public Guid CarrierId { get; set; }
    public Guid? WritingCompanyId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public string? State { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PolicyNumberPreviewRequestDto
{
    public string Format { get; set; } = "POL-{YYYY}-{SEQ:00000}";
    public long NextNumber { get; set; } = 1;
    public string TermSuffixFormat { get; set; } = "-{TERM:00}";
    public PolicyLineOfBusiness LineOfBusiness { get; set; } = PolicyLineOfBusiness.InlandMarine;
    public string? State { get; set; }
    public string? CarrierName { get; set; }
    public int Count { get; set; } = 5;
}

public class PolicyNumberPreviewDto
{
    public IReadOnlyList<string> Numbers { get; set; } = [];
}
