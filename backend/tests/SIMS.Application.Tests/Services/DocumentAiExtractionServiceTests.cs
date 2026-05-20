using Microsoft.Extensions.Configuration;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Infrastructure.Services;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class DocumentAiExtractionServiceTests
{
    [Fact]
    public void FromConfiguration_UsesDocumentAiSectionAndBuildsProcessorName()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentAI:ProjectId"] = "smmims",
                ["DocumentAI:Location"] = "us",
                ["DocumentAI:ProcessorId"] = "597da458fedfb38b",
                ["DocumentAI:CredentialsJson"] = "{}"
            })
            .Build();

        var settings = DocumentAiSettings.FromConfiguration(configuration);

        Assert.Equal("projects/smmims/locations/us/processors/597da458fedfb38b", settings.ProcessorName);
    }

    [Fact]
    public void FromConfiguration_FallsBackToLocalEnvironmentKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DOCUMENTAI_PROJECT_ID"] = "smmims",
                ["DOCUMENTAI_LOCATION"] = "us",
                ["DOCUMENTAI_PROCESSOR_ID"] = "597da458fedfb38b",
                ["GOOGLE_APPLICATION_CREDENTIALS_JSON"] = "{}"
            })
            .Build();

        var settings = DocumentAiSettings.FromConfiguration(configuration);

        Assert.Equal("smmims", settings.ProjectId);
        Assert.Equal("us", settings.Location);
        Assert.Equal("597da458fedfb38b", settings.ProcessorId);
        Assert.Equal("{}", settings.CredentialsJson);
    }

    [Fact]
    public void Summarize_RequiresManualReviewWhenAFieldConfidenceIsLow()
    {
        var fields = new[]
        {
            new DocumentAiExtractedField("insured_name", "Tek Services", 0.98f, 1),
            new DocumentAiExtractedField("policy_number", "UNKNOWN", 0.41f, 1)
        };

        var result = DocumentAiExtractionMapper.Summarize(
            processorName: "projects/smmims/locations/us/processors/597da458fedfb38b",
            text: "sample",
            fields: fields,
            confidenceThreshold: 0.75f);

        Assert.True(result.RequiresManualReview);
        Assert.Equal(0.695f, result.AverageConfidence, precision: 3);
        Assert.Contains(result.Fields, f => f.Name == "policy_number" && f.RequiresReview);
    }
}
