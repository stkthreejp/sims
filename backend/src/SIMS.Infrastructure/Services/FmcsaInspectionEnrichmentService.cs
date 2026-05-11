using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Fmcsa;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities.Fmcsa;
using SIMS.Infrastructure.Data;

namespace SIMS.Infrastructure.Services;

public class FmcsaInspectionEnrichmentService : IFmcsaInspectionEnrichmentService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IGeocodingService _geocoding;
    private readonly ILogger<FmcsaInspectionEnrichmentService> _logger;

    public FmcsaInspectionEnrichmentService(
        ApplicationDbContext db,
        IHttpClientFactory httpFactory,
        IGeocodingService geocoding,
        ILogger<FmcsaInspectionEnrichmentService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _geocoding = geocoding;
        _logger = logger;
    }

    public async Task<Result<FmcsaInspectionEnrichmentDto>> EnrichRecentInspectionsAsync(int maxInspections = 50, CancellationToken ct = default)
    {
        maxInspections = Math.Clamp(maxInspections, 1, 500);
        var inspections = await _db.FmcsaInspections
            .OrderByDescending(i => i.InspectionDate)
            .Where(i =>
                i.DetailEnrichedAt == null ||
                (i.Latitude == null && i.State != null && (i.InspectionLocation != null || i.InspectionCounty != null || i.CountyCode != null)))
            .Take(maxInspections)
            .ToListAsync(ct);

        var result = new FmcsaInspectionEnrichmentDto
        {
            InspectionsChecked = inspections.Count,
        };

        foreach (var inspection in inspections)
        {
            ct.ThrowIfCancellationRequested();

            var updated = ApplyKnownSummaryFields(inspection);
            var detail = await TryFetchDetailAsync(inspection, ct);
            if (detail != null)
            {
                result.DetailPagesFound++;
                updated |= ApplyDetail(inspection, detail);
            }

            if (await TryGeocodeInspectionAsync(inspection, ct))
            {
                result.GeocodedCount++;
                updated = true;
            }

            inspection.DetailEnrichedAt = DateTime.UtcNow;
            if (updated)
                result.InspectionsUpdated++;
            else
                result.SkippedCount++;
        }

        await _db.SaveChangesAsync(ct);
        result.Message = result.DetailPagesFound > 0
            ? "Inspection details enriched from public SMS detail pages where available."
            : "Inspection rows were prepared for enrichment. Public SMS detail pages require an SMS event id, so county/location detail will populate when a source URL or resolvable detail link is available.";

        return Result<FmcsaInspectionEnrichmentDto>.Success(result);
    }

    private static bool ApplyKnownSummaryFields(FmcsaInspection inspection)
    {
        var updated = false;
        var levelDescription = inspection.InspectionLevel switch
        {
            1 => "I - Full",
            2 => "II - Walk-Around",
            3 => "III - Driver-Only",
            4 => "IV - Special Study",
            5 => "V - Vehicle-Only",
            6 => "VI - Radioactive Materials",
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(inspection.InspectionLevelDescription) && levelDescription != null)
        {
            inspection.InspectionLevelDescription = levelDescription;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(inspection.InspectionCounty))
        {
            var county = FmcsaCountyLookup.GetCountyName(inspection.CountyCodeState ?? inspection.State, inspection.CountyCode);
            if (!string.IsNullOrWhiteSpace(county))
            {
                inspection.InspectionCounty = county;
                updated = true;
            }
        }

        return updated;
    }

    private async Task<InspectionDetail?> TryFetchDetailAsync(FmcsaInspection inspection, CancellationToken ct)
    {
        var url = inspection.DetailSourceUrl;
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 SIMS-AutoSafety/1.0");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInformation("FMCSA inspection detail fetch skipped for {ReportNumber}: {StatusCode}", inspection.ReportNumber, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            return ParseInspectionDetail(html);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            _logger.LogInformation(ex, "FMCSA inspection detail page unavailable for {ReportNumber}", inspection.ReportNumber);
            return null;
        }
    }

    private static bool ApplyDetail(FmcsaInspection inspection, InspectionDetail detail)
    {
        var updated = false;
        updated |= SetIfPresent(v => inspection.InspectionCounty = v, inspection.InspectionCounty, detail.County);
        updated |= SetIfPresent(v => inspection.InspectionLocation = v, inspection.InspectionLocation, detail.Location);
        updated |= SetIfPresent(v => inspection.InspectionFacility = v, inspection.InspectionFacility, detail.Facility);
        updated |= SetIfPresent(v => inspection.StartTime = v, inspection.StartTime, detail.StartTime);
        updated |= SetIfPresent(v => inspection.EndTime = v, inspection.EndTime, detail.EndTime);
        updated |= SetIfPresent(v => inspection.InspectionLevelDescription = v, inspection.InspectionLevelDescription, detail.LevelDescription);

        if (inspection.PostCrash == null && detail.PostCrash != null)
        {
            inspection.PostCrash = detail.PostCrash;
            updated = true;
        }

        if (inspection.HazmatPlacardRequired == null && detail.HazmatPlacardRequired != null)
        {
            inspection.HazmatPlacardRequired = detail.HazmatPlacardRequired;
            updated = true;
        }

        return updated;
    }

    private async Task<bool> TryGeocodeInspectionAsync(FmcsaInspection inspection, CancellationToken ct)
    {
        if (inspection.Latitude != null && inspection.Longitude != null)
            return false;

        if (string.IsNullOrWhiteSpace(inspection.State))
            return false;

        var candidates = BuildGeocodeCandidates(inspection);
        foreach (var candidate in candidates)
        {
            var geocode = await _geocoding.GeocodeAsync(candidate.Request, ct);
            if (geocode == null)
                continue;

            inspection.Latitude = geocode.Latitude;
            inspection.Longitude = geocode.Longitude;
            inspection.GeocodePrecision = string.IsNullOrWhiteSpace(geocode.Precision)
                ? candidate.Precision
                : $"{candidate.Precision}:{geocode.Precision}";
            return true;
        }

        return false;
    }

    private static List<GeocodeCandidate> BuildGeocodeCandidates(FmcsaInspection inspection)
    {
        var candidates = new List<GeocodeCandidate>();
        var county = inspection.InspectionCounty;
        var state = inspection.State ?? string.Empty;

        if (HasUsableInspectionLocation(inspection.InspectionLocation))
        {
            candidates.Add(new GeocodeCandidate(
                "Inspection location",
                new GeocodeRequest(
                    inspection.InspectionLocation!,
                    null,
                    !string.IsNullOrWhiteSpace(county) ? $"{county} County" : string.Empty,
                    state,
                    string.Empty)));
        }

        if (!string.IsNullOrWhiteSpace(county))
        {
            candidates.Add(new GeocodeCandidate(
                "County estimate",
                new GeocodeRequest(
                    $"{county} County",
                    null,
                    string.Empty,
                    state,
                    string.Empty)));
        }

        return candidates;
    }

    private static bool HasUsableInspectionLocation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim().ToUpperInvariant();
        return normalized is not "NOT REPORTED" and not "NOT LISTED" and not "ROAD SIDE" and not "ROADSIDE";
    }

    private sealed record GeocodeCandidate(string Precision, GeocodeRequest Request);

    private static InspectionDetail ParseInspectionDetail(string html)
    {
        var text = WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " "));
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return new InspectionDetail
        {
            County = ReadLabel(text, "Inspection County"),
            Location = ReadLabel(text, "Inspection Location"),
            Facility = ReadLabel(text, "Inspection Facility"),
            StartTime = ReadCompoundLabel(text, "Start-End Time", true),
            EndTime = ReadCompoundLabel(text, "Start-End Time", false),
            LevelDescription = ReadLabel(text, "Inspection Level"),
            PostCrash = ReadYesNo(ReadLabel(text, "Post Crash")),
            HazmatPlacardRequired = ReadYesNo(ReadLabel(text, "Hazmat Placard Required")),
        };
    }

    private static string? ReadLabel(string text, string label)
    {
        var pattern = $@"{Regex.Escape(label)}\s*:\s*(?<value>.*?)(?=\s+[A-Z][A-Za-z\-/ ]{{2,35}}\s*:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? CleanValue(match.Groups["value"].Value) : null;
    }

    private static string? ReadCompoundLabel(string text, string label, bool firstPart)
    {
        var value = ReadLabel(text, label);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var parts = value.Split('-', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return firstPart ? parts.FirstOrDefault() : parts.Skip(1).FirstOrDefault();
    }

    private static bool? ReadYesNo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.StartsWith("Y", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.StartsWith("N", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static string? CleanValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        return cleaned.Length > 0 ? cleaned : null;
    }

    private static bool SetIfPresent(Action<string> set, string? current, string? next)
    {
        if (string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(next))
        {
            set(next);
            return true;
        }

        return false;
    }

    private sealed class InspectionDetail
    {
        public string? County { get; init; }
        public string? Location { get; init; }
        public string? Facility { get; init; }
        public string? StartTime { get; init; }
        public string? EndTime { get; init; }
        public string? LevelDescription { get; init; }
        public bool? PostCrash { get; init; }
        public bool? HazmatPlacardRequired { get; init; }
    }
}
