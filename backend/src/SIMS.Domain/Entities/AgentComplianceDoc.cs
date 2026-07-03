namespace SIMS.Domain.Entities;

public enum AgentComplianceDocType
{
    EOCertificate,
    StateLicense,
    BrokerAgreement,
}

public class AgentComplianceDoc : BaseEntity
{
    public Guid AgentId { get; set; }
    public AgentComplianceDocType DocType { get; set; }

    // EOCertificate / StateLicense
    public DateOnly? ExpirationDate { get; set; }

    // EOCertificate only
    public decimal? EoLimit { get; set; }
    public string? EoCarrierName { get; set; }

    // StateLicense only
    public string? LicenseState { get; set; }

    // BrokerAgreement only
    public DateOnly? ExecutedDate { get; set; }
    public bool IsContinuous { get; set; }

    public string? Notes { get; set; }

    public Agent Agent { get; set; } = null!;
}
