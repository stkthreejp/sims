using Microsoft.Extensions.Configuration;

namespace SIMS.Application.Configuration;

public class DocumentAiSettings
{
    public string ProjectId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ProcessorId { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = string.Empty;
    public float ConfidenceThreshold { get; set; } = 0.75f;

    public string ProcessorName => $"projects/{ProjectId}/locations/{Location}/processors/{ProcessorId}";

    public static DocumentAiSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("DocumentAI");
        var settings = new DocumentAiSettings
        {
            ProjectId = FirstConfigured(section["ProjectId"], configuration["DOCUMENTAI_PROJECT_ID"]),
            Location = FirstConfigured(section["Location"], configuration["DOCUMENTAI_LOCATION"]),
            ProcessorId = FirstConfigured(section["ProcessorId"], configuration["DOCUMENTAI_PROCESSOR_ID"]),
            CredentialsJson = FirstConfigured(
                section["CredentialsJson"],
                configuration["DOCUMENTAI_CREDENTIALS_JSON"],
                configuration["GOOGLE_APPLICATION_CREDENTIALS_JSON"])
        };

        if (float.TryParse(FirstConfigured(section["ConfidenceThreshold"], configuration["DOCUMENTAI_CONFIDENCE_THRESHOLD"]), out var threshold))
            settings.ConfidenceThreshold = threshold;

        return settings;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
            throw new InvalidOperationException("DocumentAI:ProjectId is not configured.");
        if (string.IsNullOrWhiteSpace(Location))
            throw new InvalidOperationException("DocumentAI:Location is not configured.");
        if (string.IsNullOrWhiteSpace(ProcessorId))
            throw new InvalidOperationException("DocumentAI:ProcessorId is not configured.");
        if (string.IsNullOrWhiteSpace(CredentialsJson))
            throw new InvalidOperationException("DocumentAI:CredentialsJson is not configured.");
    }

    private static string FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;
}
