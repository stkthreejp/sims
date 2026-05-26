using SIMS.Domain.Enums;

namespace SIMS.Application.DTOs.Carriers;

// ─── Response DTOs ────────────────────────────────────────────────────────────

public class CarrierContactDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class CarrierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Naic { get; set; }
    public string? AmBestRating { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Website { get; set; }
    public string DefaultCurrencyCode { get; set; } = "USD";
    public bool IsActive { get; set; }
    public List<PolicyLineOfBusiness> LinesOfBusiness { get; set; } = new();
    public List<CarrierContactDto> Contacts { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CarrierListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Naic { get; set; }
    public string? AmBestRating { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public bool IsActive { get; set; }
    public List<PolicyLineOfBusiness> LinesOfBusiness { get; set; } = new();
    public int ContactCount { get; set; }
}

// ─── Input DTOs ───────────────────────────────────────────────────────────────

public class CarrierContactInputDto
{
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? Title { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
}

public class CarrierCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Naic { get; set; }
    public string? AmBestRating { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Website { get; set; }
    public string? DefaultCurrencyCode { get; set; }
    public List<PolicyLineOfBusiness> LinesOfBusiness { get; set; } = new();
}

public class CarrierUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Naic { get; set; }
    public string? AmBestRating { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Website { get; set; }
    public string? DefaultCurrencyCode { get; set; }
    public bool IsActive { get; set; } = true;
    public List<PolicyLineOfBusiness> LinesOfBusiness { get; set; } = new();
}
