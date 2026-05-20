using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AiGuidelineControlProposalServiceTests
{
    [Fact]
    public async Task ProposeFromTextAsync_CreatesDocumentAndAiSuggestedControlsOnly()
    {
        await using var db = CreateDb();
        var guidelineService = new UnderwritingGuidelineControlService(db);
        var service = new AiGuidelineControlProposalService(guidelineService);
        var userId = Guid.NewGuid();

        var result = await service.ProposeFromTextAsync(new AiGuidelineControlProposalRequest(
            Document: new CreateUnderwritingGuidelineDocumentRequest(
                ProgramName: "Longleaf",
                CarrierId: null,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                StateCode: "ALL",
                Title: "Longleaf Inland Marine UW Guidelines",
                SourceFileName: "longleaf-im-guidelines.pdf",
                SourceBlobName: "guidelines/longleaf-im-guidelines.pdf",
                Notes: "Imported by AI for human review"),
            GuidelineText: """
                Five years currently valued loss runs are required for underwriting review.
                Signed application is required before bind.
                Referral required for any single piece of equipment over $500,000.
                """), userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Longleaf Inland Marine UW Guidelines", result.Value!.Document.Title);
        Assert.Equal(3, result.Value.Controls.Count);

        var lossRuns = result.Value.Controls.Single(c => c.RuleKey == "five-year-loss-runs");
        Assert.Equal(UnderwritingControlItemType.DocumentChecklistItem, lossRuns.ItemType);
        Assert.Equal(UnderwritingControlStage.Submission, lossRuns.Stage);
        Assert.Equal(UnderwritingControlSeverity.Warning, lossRuns.Severity);
        Assert.False(lossRuns.IsBlocking);
        Assert.Equal(UnderwritingControlStatus.AiSuggested, lossRuns.Status);

        var signedApplication = result.Value.Controls.Single(c => c.RuleKey == "signed-application");
        Assert.Equal(UnderwritingControlStage.Bind, signedApplication.Stage);
        Assert.Equal(UnderwritingControlSeverity.HardBlock, signedApplication.Severity);
        Assert.True(signedApplication.IsBlocking);
        Assert.Null(signedApplication.PublishedAt);

        var pieceReferral = result.Value.Controls.Single(c => c.RuleKey == "single-piece-over-500k");
        Assert.Equal(UnderwritingControlItemType.ReferralTrigger, pieceReferral.ItemType);
        Assert.Equal(UnderwritingControlSeverity.ReferralRequired, pieceReferral.Severity);
        Assert.Contains("\"amount\":500000", pieceReferral.ConditionJson);

        Assert.Empty(await db.Set<UnderwritingGuidelineControl>()
            .Where(c => c.Status == UnderwritingControlStatus.Published)
            .ToListAsync());
    }

    [Fact]
    public async Task ProposeFromTextAsync_RequiresGuidelineText()
    {
        var guidelineService = new UnderwritingGuidelineControlService(CreateDb());
        var service = new AiGuidelineControlProposalService(guidelineService);

        var result = await service.ProposeFromTextAsync(new AiGuidelineControlProposalRequest(
            Document: new CreateUnderwritingGuidelineDocumentRequest(
                ProgramName: "Longleaf",
                CarrierId: null,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                StateCode: "ALL",
                Title: "Longleaf Inland Marine UW Guidelines",
                SourceFileName: null,
                SourceBlobName: null,
                Notes: null),
            GuidelineText: " "), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("GUIDELINE_TEXT_REQUIRED", result.ErrorCode);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
