using System.ComponentModel.DataAnnotations;
using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Insureds;

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
    public string? UsDotNumber { get; set; }
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
    [Required]
    public InsuredType InsuredType { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(200)]
    public string? Dba { get; set; }

    public BusinessEntityType? EntityType { get; set; }

    [Range(0, 200)]
    public int? YearsInBusiness { get; set; }

    [MaxLength(20)]
    public string? UsDotNumber { get; set; }

    [MaxLength(20)]
    public string? TaxId { get; set; }

    [EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [Phone, MaxLength(30)]
    public string? Phone { get; set; }

    [Phone, MaxLength(30)]
    public string? PhoneAlt { get; set; }

    [Required, MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [Required, MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(2, MinimumLength = 2)]
    public string State { get; set; } = string.Empty;

    [Required, RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "ZIP code must be 5 digits or ZIP+4 format.")]
    public string ZipCode { get; set; } = string.Empty;

    [MaxLength(100)]
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
