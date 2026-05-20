using Microsoft.EntityFrameworkCore;
using SIMS.Application.DTOs.DocumentAI;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using SIMS.Infrastructure.Services;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class DocumentAiPreviewServiceTests
{
    [Fact]
    public async Task PreviewSubmissionAttachmentAsync_ReturnsNormalizedPreviewWithoutWritingLossRows()
    {
        await using var db = CreateDb();
        var submissionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        db.Submissions.Add(new Submission { Id = submissionId, CreatedById = Guid.NewGuid(), UnderwriterId = Guid.NewGuid() });
        db.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            EntityType = DocumentEntityType.Submission,
            SubmissionId = submissionId,
            FileName = "loss-run.pdf",
            BlobPath = "submissions/loss-run.pdf",
            ContentType = "application/pdf",
            UploadedById = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var blob = new FakeBlobStorageService([1, 2, 3]);
        var extractor = new FakeDocumentAiExtractionService(new DocumentAiExtractionResult
        {
            Fields =
            [
                new("Line of Business:", "Timber Package", 0.76f, 1),
                new("As of:", "12/31/2025", 0.75f, 1),
                new("Term:", "04/10/2024 - 04/10/2025", 0.69f, 1) { RequiresReview = true },
                new("Reserve", "$0.00", 0.40f, 1) { RequiresReview = true },
                new("Expense", "$0.00", 0.38f, 1) { RequiresReview = true },
                new("Falls Lake National Insurance Company", "TMB000175201", 0.40f, 1) { RequiresReview = true }
            ]
        });
        var service = new DocumentAiPreviewService(db, blob, extractor);

        var result = await service.PreviewSubmissionAttachmentAsync(submissionId, attachmentId);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], extractor.LastContent);
        Assert.Equal("application/pdf", extractor.LastMimeType);
        Assert.Single(result.Value!.LossYears);
        Assert.Equal(2024, result.Value.LossYears[0].PolicyYear);
        Assert.Equal("TMB000175201", result.Value.LossYears[0].PolicyNumber);
        Assert.Empty(await db.SubmissionLossYears.ToListAsync());
        Assert.Empty(await db.SubmissionLossClaims.ToListAsync());
    }

    [Fact]
    public async Task PreviewSubmissionAttachmentAsync_RejectsNonPdfAttachments()
    {
        await using var db = CreateDb();
        var submissionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        db.Submissions.Add(new Submission { Id = submissionId, CreatedById = Guid.NewGuid(), UnderwriterId = Guid.NewGuid() });
        db.Attachments.Add(new Attachment
        {
            Id = attachmentId,
            EntityType = DocumentEntityType.Submission,
            SubmissionId = submissionId,
            FileName = "loss-run.xlsx",
            BlobPath = "submissions/loss-run.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            UploadedById = Guid.NewGuid()
        });
        await db.SaveChangesAsync();

        var service = new DocumentAiPreviewService(
            db,
            new FakeBlobStorageService([1]),
            new FakeDocumentAiExtractionService(new DocumentAiExtractionResult()));

        var result = await service.PreviewSubmissionAttachmentAsync(submissionId, attachmentId);

        Assert.False(result.IsSuccess);
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", result.ErrorCode);
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
        public Task<string> UploadAsync(Stream content, string fileName, string contentType) => throw new NotImplementedException();
        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) => throw new NotImplementedException();
        public Task<byte[]> DownloadAsync(string blobPath) => Task.FromResult(content);
        public Task DeleteAsync(string blobPath) => throw new NotImplementedException();
    }

    private sealed class FakeDocumentAiExtractionService(DocumentAiExtractionResult result) : IDocumentAiExtractionService
    {
        public byte[]? LastContent { get; private set; }
        public string? LastMimeType { get; private set; }

        public Task<DocumentAiExtractionResult> ProcessAsync(byte[] content, string mimeType, string fileName, CancellationToken cancellationToken = default)
        {
            LastContent = content;
            LastMimeType = mimeType;
            return Task.FromResult(result);
        }
    }
}
