using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SIMS.Application.Configuration;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Xunit;

namespace SIMS.Application.Tests.Services;

public class IntakeProcessingServiceTests
{
    [Fact]
    public async Task ProcessNextAsync_Completes_PersistsLinesOfBusinessAndQuotingLine()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Queued });
        await db.SaveChangesAsync();

        var analysis = new SubmissionAnalysis
        {
            Boundaries = { new FormSpan { StartPage = 1, EndPage = 1, Form = "Acord126", LineOfBusiness = "GeneralLiability" } },
            QuotingLineOfBusiness = "GeneralLiability",
            PerLob = { new DocumentLobExtraction("GeneralLiability", new DocumentExtractionResult()) },
            Confidence = "High",
        };
        var service = Service(db, analysis: analysis, pagesPerPdf: 1);

        Assert.True(await service.ProcessNextAsync());

        var job = await db.Set<IntakeJob>().SingleAsync();
        Assert.Equal(IntakeJobStatus.Completed, job.Status);
        Assert.NotNull(job.ResultJson);
        Assert.NotNull(job.CompletedAt);
        var reloaded = await db.Set<Submission>().SingleAsync(s => s.Id == submission.Id);
        Assert.Contains("GeneralLiability", reloaded.LinesOfBusiness);
        Assert.Equal("GeneralLiability", reloaded.QuotingLineOfBusiness);
    }

    [Fact]
    public async Task ProcessNextAsync_LowConfidence_MarksNeedsReview()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Queued });
        await db.SaveChangesAsync();

        var analysis = new SubmissionAnalysis
        {
            Boundaries = { new FormSpan { StartPage = 1, EndPage = 1, Form = "Other", LineOfBusiness = "GeneralLiability" } },
            Confidence = "Low",
        };
        await Service(db, analysis: analysis, pagesPerPdf: 1).ProcessNextAsync();

        Assert.Equal(IntakeJobStatus.NeedsReview, (await db.Set<IntakeJob>().SingleAsync()).Status);
    }

    [Fact]
    public async Task ProcessNextAsync_NullAnalysis_MarksFailed_SubmissionUntouched()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Queued });
        await db.SaveChangesAsync();

        await Service(db, analysis: null, pagesPerPdf: 1).ProcessNextAsync();

        var job = await db.Set<IntakeJob>().SingleAsync();
        Assert.Equal(IntakeJobStatus.Failed, job.Status);
        Assert.Null((await db.Set<Submission>().SingleAsync(s => s.Id == submission.Id)).LinesOfBusiness);
    }

    [Fact]
    public async Task ProcessNextAsync_NoRenderablePages_MarksNeedsReview()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Queued });
        await db.SaveChangesAsync();

        await Service(db, analysis: new SubmissionAnalysis(), pagesPerPdf: 0).ProcessNextAsync();

        Assert.Equal(IntakeJobStatus.NeedsReview, (await db.Set<IntakeJob>().SingleAsync()).Status);
    }

    [Fact]
    public async Task ProcessNextAsync_EmptyQueue_ReturnsFalse()
    {
        await using var db = CreateDb();
        Assert.False(await Service(db, analysis: new SubmissionAnalysis(), pagesPerPdf: 1).ProcessNextAsync());
    }

    [Fact]
    public async Task RequeueAsync_WhenDisabled_ReturnsFailure()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        await db.SaveChangesAsync();

        var result = await Service(db, analysis: null, pagesPerPdf: 0, enabled: false).RequeueAsync(submission.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("INTAKE_DISABLED", result.ErrorCode);
        Assert.Empty(db.Set<IntakeJob>());
    }

    [Fact]
    public async Task RequeueAsync_WhenEnabled_QueuesJob()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        await db.SaveChangesAsync();

        var result = await Service(db, analysis: null, pagesPerPdf: 0).RequeueAsync(submission.Id);

        Assert.True(result.IsSuccess);
        var job = await db.Set<IntakeJob>().SingleAsync();
        Assert.Equal(IntakeJobStatus.Queued, job.Status);
        Assert.Equal(submission.Id, job.SubmissionId);
    }

    [Fact]
    public async Task GetLatestForSubmissionAsync_ReturnsMostRecentJob_OrNull()
    {
        await using var db = CreateDb();
        var submission = SeedSubmissionWithPdf(db);
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Failed, CreatedAt = DateTime.UtcNow.AddMinutes(-10) });
        db.Set<IntakeJob>().Add(new IntakeJob { SubmissionId = submission.Id, Status = IntakeJobStatus.Completed, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var svc = Service(db, analysis: null, pagesPerPdf: 0);
        var latest = await svc.GetLatestForSubmissionAsync(submission.Id);
        Assert.NotNull(latest);
        Assert.Equal("Completed", latest!.Status);
        Assert.Null(await svc.GetLatestForSubmissionAsync(Guid.NewGuid()));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IntakeProcessingService Service(ApplicationDbContext db, SubmissionAnalysis? analysis, int pagesPerPdf, bool enabled = true) =>
        new(db,
            new FakeRenderer(pagesPerPdf),
            new FakeAnalyzer(analysis),
            new FakeBlob(),
            Options.Create(new IntakeSettings { Enabled = enabled }),
            NullLogger<IntakeProcessingService>.Instance);

    private static Submission SeedSubmissionWithPdf(ApplicationDbContext db)
    {
        var submission = new Submission
        {
            SubmissionNumber = "SUB-TEST-0001",
            InsuredId = Guid.NewGuid(),
            UnderwriterId = Guid.NewGuid(),
            CreatedById = Guid.NewGuid(),
            Status = SubmissionStatus.New,
        };
        db.Set<Submission>().Add(submission);
        db.Set<Attachment>().Add(new Attachment
        {
            SubmissionId = submission.Id,
            EntityType = DocumentEntityType.Submission,
            DocumentType = DocumentType.Application,
            FileName = "app.pdf",
            BlobPath = "blob/app.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 3,
            UploadedById = Guid.NewGuid(),
        });
        return submission;
    }

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class FakeRenderer(int pageCount) : IPdfPageRenderer
    {
        public IReadOnlyList<byte[]> RenderPdfToPngPages(byte[] pdfBytes, CancellationToken ct = default) =>
            Enumerable.Range(0, pageCount).Select(_ => new byte[] { 1 }).ToList();
    }

    private sealed class FakeAnalyzer(SubmissionAnalysis? result) : ISubmissionIntakeAnalyzer
    {
        public Task<SubmissionAnalysis?> AnalyzeSubmissionAsync(IReadOnlyList<RenderedPage> pages, string? emailBodyContext, CancellationToken ct = default) =>
            Task.FromResult(result);
    }

    private sealed class FakeBlob : IBlobStorageService
    {
        public Task<string> UploadAsync(Stream content, string fileName, string contentType) => Task.FromResult("blob/x");
        public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null) => Task.FromResult("http://blob/x");
        public Task<byte[]> DownloadAsync(string blobPath) => Task.FromResult(new byte[] { 1, 2, 3 });
        public Task DeleteAsync(string blobPath) => Task.CompletedTask;
    }
}
