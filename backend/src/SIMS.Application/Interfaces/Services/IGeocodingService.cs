namespace SIMS.Application.Interfaces.Services;

public record GeocodeRequest(
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string ZipCode);

public class GeocodeResult
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string? Precision { get; set; }
    public string Provider { get; set; } = "Google";
    public string? GooglePlaceId { get; set; }
}

public interface IGeocodingService
{
    Task<GeocodeResult?> GeocodeAsync(GeocodeRequest request, CancellationToken ct = default);
}
