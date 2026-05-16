using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Quotes;

public class QuoteDto
{
    public Guid Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public Guid InsuredId { get; set; }
    public string InsuredName { get; set; } = string.Empty;
    public Guid CarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public QuoteStatus Status { get; set; }
    public string? PolicyNumber { get; set; }
    public Guid? BoundPolicyId { get; set; }
    public DateOnly? BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }

    // Commission rates from schedules
    public decimal CarrierCommissionRate { get; set; }
    public decimal SMMRetentionRate { get; set; }
    public decimal AgentCommissionRate { get; set; }

    // Computed dollar amounts (rate × premium)
    public decimal CarrierCommissionAmount { get; set; }
    public decimal SMMRetentionAmount { get; set; }
    public decimal AgentCommissionAmount { get; set; }

    // Commission give-back override
    public CommissionOverrideDto? CommissionOverride { get; set; }

    public int? CompanyId { get; set; }
    public int? ProducerId { get; set; }
    public bool IsFilingState { get; set; }

    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public decimal? UninsuredMotoristLimit { get; set; }
    public decimal? MedicalPaymentsLimit { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CommissionOverrideDto
{
    public decimal CarrierRate { get; set; }
    public decimal SMMRate { get; set; }
    public decimal AgentRate { get; set; }
    public Guid OverrideBy { get; set; }
    public DateTime OverrideAt { get; set; }

    // Computed dollar amounts at the overridden rates
    public decimal CarrierCommissionAmount { get; set; }
    public decimal SMMRetentionAmount { get; set; }
    public decimal AgentCommissionAmount { get; set; }
}

public class QuoteListItemDto
{
    public Guid Id { get; set; }
    public string QuoteNumber { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public string SubmissionNumber { get; set; } = string.Empty;
    public string InsuredName { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public QuoteStatus Status { get; set; }
    public string? PolicyNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal TotalPremium { get; set; }
    public bool HasCommissionOverride { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuoteCreateDto
{
    public Guid SubmissionId { get; set; }
    public Guid CarrierId { get; set; }
    public PolicyLineOfBusiness LineOfBusiness { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public int? CompanyId { get; set; }
    public int? ProducerId { get; set; }
    public bool IsFilingState { get; set; }

    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public decimal? UninsuredMotoristLimit { get; set; }
    public decimal? MedicalPaymentsLimit { get; set; }
}

public class QuoteUpdateDto : QuoteCreateDto
{
    public QuoteStatus Status { get; set; }
}

public class QuoteBindDto
{
    public DateOnly BoundDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
}

public class CommissionOverrideRequest
{
    // Exactly one of these must be provided
    public decimal? GivebackAmount { get; set; }   // dollar amount agent gives back
    public decimal? NewAgentRate { get; set; }      // new agent rate as decimal (e.g. 0.08)
}
