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
    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
    public string? GeocodePrecision { get; set; }
    public string? GeocodeProvider { get; set; }
    public string? GooglePlaceId { get; set; }
    public DateTime? GeocodedAt { get; set; }
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

    [Required, EmailAddress, MaxLength(256)]
    public string? Email { get; set; }

    [Required, Phone, MaxLength(30)]
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

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    [MaxLength(50)]
    public string? GeocodePrecision { get; set; }

    [MaxLength(50)]
    public string? GeocodeProvider { get; set; }

    [MaxLength(200)]
    public string? GooglePlaceId { get; set; }
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
    public DateOnly? NearestPolicyExpiration { get; set; }
    public bool HasCancelledPolicy { get; set; }
}

public class InsuredSummaryStatsDto
{
    public int TotalInsureds { get; set; }
    public int ActivePolicies { get; set; }
    public int ExpiringPolicies90d { get; set; }
    public int RecentCancellations { get; set; }
}
