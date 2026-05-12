namespace SIMS.Application.Configuration;

public class LegiScanSettings
{
    public string BaseUrl { get; set; } = "https://api.legiscan.com";
    public string? ApiKey { get; set; }
    public int MaxMonitoredBills { get; set; } = 50;
    public int MonthlyQueryLimit { get; set; } = 30000;
}
