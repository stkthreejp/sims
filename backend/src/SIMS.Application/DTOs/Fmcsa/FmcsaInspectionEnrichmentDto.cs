namespace SIMS.Application.DTOs.Fmcsa;

public class FmcsaInspectionEnrichmentDto
{
    public int InspectionsChecked { get; set; }
    public int InspectionsUpdated { get; set; }
    public int DetailPagesFound { get; set; }
    public int GeocodedCount { get; set; }
    public int SkippedCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime RefreshedAt { get; set; } = DateTime.UtcNow;
}
