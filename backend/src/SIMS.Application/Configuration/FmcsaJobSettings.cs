namespace SIMS.Application.Configuration;

public class FmcsaJobSettings
{
    public bool Enabled { get; set; }
    public bool RunImportedCarrierAnalytics { get; set; } = true;
    public bool RunInspectionEnrichment { get; set; } = true;
    public bool RunOfficialSmsPeerImport { get; set; }
    public string DailyRunTimeUtc { get; set; } = "06:00";
    public int MonthlySmsImportDay { get; set; } = 2;
    public string MonthlySmsImportTimeUtc { get; set; } = "07:00";
    public int InspectionEnrichmentMaxRows { get; set; } = 250;
}
