using IMS.Domain.Enums;

namespace IMS.Application.DTOs.Insureds;

public class InsuredDto
{
    public Guid Id { get; set; }
    public InsuredType InsuredType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? CompanyName { get; set; }
    public string? Dba { get; set; }
    public BusinessEntityType? EntityType { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneAlt { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? County { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int PolicyCount { get; set; }
}

public class InsuredCreateDto
{
    public InsuredType InsuredType { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? CompanyName { get; set; }
    public string? Dba { get; set; }
    public BusinessEntityType? EntityType { get; set; }
    public int? YearsInBusiness { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PhoneAlt { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string? County { get; set; }
}

public class InsuredUpdateDto : InsuredCreateDto
{
    public bool IsActive { get; set; } = true;
}

public class InsuredListItemDto
{
    public Guid Id { get; set; }
    public InsuredType InsuredType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int PolicyCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
