using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Quotes;

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
    public DateOnly? BoundDate { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public DateOnly? CancelledDate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal PremiumAmount { get; set; }
    public decimal TaxesAndFees { get; set; }
    public decimal TotalPremium { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
    public DateTime CreatedAt { get; set; }
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
    public decimal CommissionRate { get; set; }
    public string? CoverageDescription { get; set; }
    public decimal? Deductible { get; set; }
    public decimal? Limit { get; set; }
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
