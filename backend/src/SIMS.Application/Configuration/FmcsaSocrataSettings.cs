namespace SIMS.Application.Configuration;

public class FmcsaSocrataSettings
{
    public string BaseUrl { get; set; } = "https://data.transportation.gov";
    public string? AppToken { get; set; }
    public string CensusDatasetId { get; set; } = "az4n-8mr2";
    public string InspectionsDatasetId { get; set; } = "fx4q-ay7w";
    public string ViolationsDatasetId { get; set; } = "876r-jsdb";
    public string CrashesDatasetId { get; set; } = "aayw-vxb3";
    public int PageSize { get; set; } = 5000;
    public int MaxRowsPerDataset { get; set; } = 25000;
}
