namespace IMS.Domain.Entities;

public class Carrier : BaseEntity
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
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<CarrierLineOfBusiness> LinesOfBusiness { get; set; } = new List<CarrierLineOfBusiness>();
    public ICollection<CarrierContact> Contacts { get; set; } = new List<CarrierContact>();
    public ICollection<Quote> Quotes { get; set; } = new List<Quote>();
}
