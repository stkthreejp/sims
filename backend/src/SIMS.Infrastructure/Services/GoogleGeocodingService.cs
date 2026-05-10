using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public class GoogleGeocodingService : IGeocodingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _configuration;

    public GoogleGeocodingService(IHttpClientFactory httpFactory, IConfiguration configuration)
    {
        _httpFactory = httpFactory;
        _configuration = configuration;
    }

    public async Task<GeocodeResult?> GeocodeAsync(GeocodeRequest request, CancellationToken ct = default)
    {
        var apiKey = _configuration["GoogleMaps:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(request.AddressLine1))
            return null;

        var address = string.Join(", ", new[]
        {
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.State,
            request.ZipCode,
            "USA"
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var client = _httpFactory.CreateClient("google_geocoding");
        var url = $"/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(apiKey)}";
        using var response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (!doc.RootElement.TryGetProperty("status", out var status) || status.GetString() != "OK")
            return null;

        var first = doc.RootElement.GetProperty("results").EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
            return null;

        var location = first.GetProperty("geometry").GetProperty("location");
        var latitude = location.GetProperty("lat").GetDecimal();
        var longitude = location.GetProperty("lng").GetDecimal();
        var precision = first.GetProperty("geometry").TryGetProperty("location_type", out var locationType)
            ? locationType.GetString()
            : null;
        var placeId = first.TryGetProperty("place_id", out var placeIdElement)
            ? placeIdElement.GetString()
            : null;

        return new GeocodeResult
        {
            Latitude = decimal.Parse(latitude.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            Longitude = decimal.Parse(longitude.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            Precision = precision,
            GooglePlaceId = placeId,
        };
    }
}
