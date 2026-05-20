using SIMS.Application.DTOs.DocumentAI;
using SIMS.Infrastructure.Services;
using SIMS.Domain.Enums;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class DocumentAiNormalizationServiceTests
{
    [Fact]
    public void Normalize_MapsAcordFieldsIntoExistingSubmissionPreviewShape()
    {
        var extraction = new DocumentAiExtractionResult
        {
            ProcessorName = "projects/smmims/locations/us/processors/597da458fedfb38b",
            Fields =
            [
                new("DESCRIPTION OF PRIMARY OPERATIONS", "Mechanized Logging", 0.54f, 2) { RequiresReview = true },
                new("INDIVIDUAL", "\u2611", 0.99f, 1),
                new("COMMERCIAL INLAND MARINE", "\u2611", 0.58f, 1) { RequiresReview = true }
            ]
        };

        var preview = DocumentAiNormalizationService.Normalize(extraction);

        Assert.Equal("Mechanized Logging", preview.SubmissionData.DescriptionOfOperations);
        Assert.Equal("Individual", preview.SubmissionData.EntityType);
        Assert.NotNull(preview.SubmissionData.IMCoverages);
        Assert.Contains(preview.FieldsRequiringReview, f => f.Name == "DESCRIPTION OF PRIMARY OPERATIONS");
        Assert.Empty(preview.LossYears);
    }

    [Fact]
    public void Normalize_MapsLossRunPagesIntoLossYearPreviewDtosWithoutWritingRows()
    {
        var extraction = new DocumentAiExtractionResult
        {
            Fields =
            [
                new("Line of Business:", "Timber Package", 0.76f, 1),
                new("As of:", "12/31/2025", 0.75f, 1),
                new("Term:", "04/10/2023 - 04/10/2024", 0.69f, 1) { RequiresReview = true },
                new("Totals:", "$0.00 $0.00$216,797.15", 0.39f, 1) { RequiresReview = true },
                new("Falls Lake National Insurance Company", "TMB000175200", 0.39f, 1) { RequiresReview = true },
                new("Line of Business:", "Timber Package", 0.76f, 2),
                new("As of:", "12/31/2025", 0.75f, 2),
                new("Term:", "04/10/2024 - 04/10/2025", 0.69f, 2) { RequiresReview = true },
                new("Reserve", "$0.00", 0.40f, 2) { RequiresReview = true },
                new("Expense", "$0.00", 0.38f, 2) { RequiresReview = true },
                new("Falls Lake National Insurance Company", "TMB000175201", 0.40f, 2) { RequiresReview = true }
            ]
        };

        var preview = DocumentAiNormalizationService.Normalize(extraction);

        Assert.Equal(2, preview.LossYears.Count);

        var first = preview.LossYears[0];
        Assert.Equal(2023, first.PolicyYear);
        Assert.Equal("Timber Package", first.LineOfBusiness);
        Assert.Equal("Falls Lake National Insurance Company", first.CarrierName);
        Assert.Equal("TMB000175200", first.PolicyNumber);
        Assert.Equal(new DateOnly(2025, 12, 31), first.AsOfDate);
        Assert.Equal(0, first.PaidOverride);
        Assert.Equal(0, first.ReservedOverride);
        Assert.Equal(216797.15m, first.ExpenseOverride);
        Assert.Equal(LossPremiumBasis.Actual, first.PremiumBasis);
        Assert.Equal("DocumentAI", first.Source);

        Assert.Contains(preview.FieldsRequiringReview, f => f.Name == "Totals:");
    }
}
