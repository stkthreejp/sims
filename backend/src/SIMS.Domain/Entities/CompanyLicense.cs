namespace SIMS.Domain.Entities;

/// <summary>
/// A surplus-lines (or other) license held by SMM or an individual broker, stored once and
/// referenced from Surplus Lines setup so the broker/license details aren't re-keyed per state.
/// </summary>
public class CompanyLicense : BaseEntity
{
    public string HolderName { get; set; } = string.Empty;   // e.g. "Specialty Market Managers, LLC" / "Jeremiah O'Donovan"
    public string LicenseNumber { get; set; } = string.Empty;
    public string LicenseState { get; set; } = string.Empty;  // 2-letter state code
    public string LicenseType { get; set; } = string.Empty;   // e.g. "Surplus Lines Broker"
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string Country { get; set; } = "USA";

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}
