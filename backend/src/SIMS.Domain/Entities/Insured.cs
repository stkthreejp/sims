using SIMS.Domain.Enums;

namespace SIMS.Domain.Entities;

public class Insured : BaseEntity
{
    public InsuredType InsuredType { get; set; }

    // Individual fields
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }

    // Commercial fields
    public string? CompanyName { get; set; }
    public string? Dba { get; set; }
    public BusinessEntityType? EntityType { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? OperationType { get; set; }
    public int? CreditScore { get; set; }
    public string? Website { get; set; }
    public string? TaxId { get; set; }

    // Contact
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneAlt { get; set; }

    // Address
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? County { get; set; }

    public bool IsActive { get; set; } = true;
    public Guid CreatedById { get; set; }

    public string DisplayName => InsuredType == InsuredType.Commercial
        ? CompanyName ?? string.Empty
        : $"{FirstName} {LastName}".Trim();

    public User CreatedBy { get; set; } = null!;
    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
