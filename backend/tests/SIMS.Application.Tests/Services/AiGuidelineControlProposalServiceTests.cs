using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Application.DTOs.Underwriting;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class AiGuidelineControlProposalServiceTests
{
    [Fact]
    public async Task ProposeFromAttachmentAsync_ExtractsPdfAndCreatesAiSuggestedControls()
    {
        await using var db = CreateDb();
        var attachmentId = Guid.NewGuid();
        db.Set<Attachment>().Add(new Attachment
        {
            Id = attachmentId,
            DocumentType = DocumentType.UnderwritingGuidelines,
            EntityType = DocumentEntityType.Carrier,
            FileName = "longleaf-guidelines.pdf",
            BlobPath = "carrier-guidelines/longleaf-guidelines.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 12,
            UploadedById = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var blob = new FakeBlobStorageService([1, 2, 3]);
        var documentAi = new FakeDocumentAiExtractionService("""
            Five years currently valued loss runs are required for underwriting review.
            Signed application is required before bind.
            """);
        var guidelineService = new UnderwritingGuidelineControlService(db);
        var service = new AiGuidelineControlProposalService(guidelineService, db, blob, documentAi);
        var userId = Guid.NewGuid();

        var result = await service.ProposeFromAttachmentAsync(new AiGuidelineControlProposalFromAttachmentRequest(
            AttachmentId: attachmentId,
            Document: new CreateUnderwritingGuidelineDocumentRequest(
                ProgramName: "Longleaf",
                CarrierId: null,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                StateCode: "ALL",
                Title: "Longleaf Inland Marine UW Guidelines",
                SourceFileName: null,
                SourceBlobName: null,
                Notes: "Imported by AI for human review")), userId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("longleaf-guidelines.pdf", result.Value!.Document.SourceFileName);
        Assert.Equal("carrier-guidelines/longleaf-guidelines.pdf", result.Value.Document.SourceBlobName);
        Assert.Equal(2, result.Value.Controls.Count);
        Assert.All(result.Value.Controls, c => Assert.Equal(UnderwritingControlStatus.AiSuggested, c.Status));
        Assert.Equal("carrier-guidelines/longleaf-guidelines.pdf", blob.DownloadedPath);
        Assert.Equal("application/pdf", documentAi.MimeType);
        Assert.Equal("longleaf-guidelines.pdf", documentAi.FileName);
    }

    [Fact]
    public async Task ProposeFromAttachmentAsync_RejectsUnsupportedGuidelineFileTypes()
    {
        await using var db = CreateDb();
        var attachmentId = Guid.NewGuid();
        db.Set<Attachment>().Add(new Attachment
        {
            Id = attachmentId,
            DocumentType = DocumentType.UnderwritingGuidelines,
            EntityType = DocumentEntityType.Carrier,
            FileName = "longleaf-guidelines.docx",
            BlobPath = "carrier-guidelines/longleaf-guidelines.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSizeBytes = 12,
            UploadedById = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = new AiGuidelineControlProposalService(
            new UnderwritingGuidelineControlService(db),
            db,
            new FakeBlobStorageService([1, 2, 3]),
            new FakeDocumentAiExtractionService("not used"));

        var result = await service.ProposeFromAttachmentAsync(new AiGuidelineControlProposalFromAttachmentRequest(
            AttachmentId: attachmentId,
            Document: new CreateUnderwritingGuidelineDocumentRequest(
                ProgramName: "Longleaf",
                CarrierId: null,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                StateCode: "ALL",
                Title: "Longleaf Inland Marine UW Guidelines",
                SourceFileName: null,
                SourceBlobName: null,
                Notes: null)), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("UNSUPPORTED_GUIDELINE_ATTACHMENT", result.ErrorCode);
    }

    [Fact]
    public async Task ProposeFromAttachmentAsync_ReturnsFailureWhenExtractionFails()
    {
        await using var db = CreateDb();
        var attachmentId = Guid.NewGuid();
        db.Set<Attachment>().Add(new Attachment
        {
            Id = attachmentId,
            DocumentType = DocumentType.UnderwritingGuidelines,
            EntityType = DocumentEntityType.Carrier,
            FileName = "lloyds-guidelines.pdf",
            BlobPath = "carrier-guidelines/lloyds-guidelines.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 12,
            UploadedById = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = new AiGuidelineControlProposalService(
            new UnderwritingGuidelineControlService(db),
            db,
            new FakeBlobStorageService([1, 2, 3]),
            new FailingDocumentAiExtractionService());

        var result = await service.ProposeFromAttachmentAsync(new AiGuidelineControlProposalFromAttachmentRequest(
            AttachmentId: attachmentId,
            Document: new CreateUnderwritingGuidelineDocumentRequest(
                ProgramName: "Lloyds",
                CarrierId: null,
                LineOfBusiness: PolicyLineOfBusiness.InlandMarine,
                StateCode: "ALL",
                Title: "Lloyds UW Guidelines",
                SourceFileName: null,
                SourceBlobName: null,
                Notes: null)), Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("GUIDELINE_ATTACHMENT_EXTRACTION_FAILED", result.ErrorCode);
    }

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
        using var condition = JsonDocument.Parse(pieceReferral.ConditionJson!);
        Assert.Equal("largestSingleItemValue", condition.RootElement.GetProperty("field").GetString());
        Assert.Equal(">", condition.RootElement.GetProperty("operator").GetString());
        Assert.Equal(500000, condition.RootElement.GetProperty("value").GetInt32());

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

    private sealed class FakeBlobStorageService(byte[] content) : IBlobStorageService
    {
        public string? DownloadedPath { get; private set; }

        public Task<string> UploadAsync(Stream content, string fileName, string contentType) =>
            Task.FromResult("not-used");

        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) =>
            Task.FromResult("not-used");

        public Task<byte[]> DownloadAsync(string blobPath)
        {
            DownloadedPath = blobPath;
            return Task.FromResult(content);
        }

        public Task DeleteAsync(string blobPath) => Task.CompletedTask;
    }

    private sealed class FakeDocumentAiExtractionService(string text) : IDocumentAiExtractionService
    {
        public string? MimeType { get; private set; }
        public string? FileName { get; private set; }

        public Task<DocumentAiExtractionResult> ProcessAsync(byte[] content, string mimeType, string fileName, CancellationToken cancellationToken = default)
        {
            MimeType = mimeType;
            FileName = fileName;
            return Task.FromResult(new DocumentAiExtractionResult { Text = text });
        }
    }

    private sealed class FailingDocumentAiExtractionService : IDocumentAiExtractionService
    {
        public Task<DocumentAiExtractionResult> ProcessAsync(byte[] content, string mimeType, string fileName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Document AI settings are incomplete.");
    }
}
