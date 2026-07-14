namespace SIMS.Application.Configuration;

/// <summary>
/// Automated submission-intake settings. <see cref="Enabled"/> is the kill-switch:
/// when false, no intake jobs are enqueued and the worker idles. Ships false — turn on
/// once verified on the test environment (design §8 / §9 trigger decision).
/// </summary>
public class IntakeSettings
{
    public bool Enabled { get; set; } = false;
    public int PollingIntervalMinutes { get; set; } = 2;
}
