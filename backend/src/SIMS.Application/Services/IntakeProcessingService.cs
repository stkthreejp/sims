using System.Text.Json;
using SIMS.Application.DTOs.DocumentExtraction;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SIMS.Application.Services;

/// <summary>
/// Processes one queued intake job: render the submission's PDF attachments → Claude
/// vision analysis → persist detected lines of business + the analysis result. Deliverable
/// filing (split PDFs), the completeness check, and the account-summary document are added
/// as later stages; the full analysis is stored on the job so nothing is lost.
/// </summary>
public class IntakeProcessingService : IIntakeProcessingService
{
    private readonly DbContext _db;
    private readonly IPdfPageRenderer _renderer;
    private readonly ISubmissionIntakeAnalyzer _analyzer;
    private readonly IBlobStorageService _blob;
    private readonly ILogger<IntakeProcessingService> _logger;

    public IntakeProcessingService(
        DbContext db,
        IPdfPageRenderer renderer,
        ISubmissionIntakeAnalyzer analyzer,
        IBlobStorageService blob,
        ILogger<IntakeProcessingService> logger)
    {
        _db = db;
        _renderer = renderer;
        _analyzer = analyzer;
        _blob = blob;
        _logger = logger;
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        var job = await _db.Set<IntakeJob>()
            .Where(j => j.Status == IntakeJobStatus.Queued)
            .OrderBy(j => j.CreatedAt).ThenBy(j => j.Id)
            .FirstOrDefaultAsync(ct);
        if (job == null) return false;

        job.Status = IntakeJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        job.AttemptCount++;
        await _db.SaveChangesAsync(ct);

        try
        {
            await RunAsync(job, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Intake job {JobId} failed.", job.Id);
            job.Status = IntakeJobStatus.Failed;
            job.ErrorMessage = Truncate(ex.Message, 2000);
            job.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    private async Task RunAsync(IntakeJob job, CancellationToken ct)
    {
        // ── Render ────────────────────────────────────────────────────────────
        await SetStageAsync(job, "Rendering", ct);

        var attachments = await _db.Set<Attachment>()
            .Where(a => a.SubmissionId == job.SubmissionId && !a.IsDeleted
                && a.EntityType == DocumentEntityType.Submission)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        var pages = new List<RenderedPage>();
        var pageNumber = 1;
        foreach (var att in attachments.Where(IsPdf))
        {
            byte[] bytes;
            try
            {
                bytes = await _blob.DownloadAsync(att.BlobPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Intake: could not download blob {Blob}.", att.BlobPath);
                continue;
            }

            foreach (var png in _renderer.RenderPdfToPngPages(bytes, ct))
                pages.Add(new RenderedPage(pageNumber++, png));
        }

        if (pages.Count == 0)
        {
            await CompleteAsync(job, IntakeJobStatus.NeedsReview,
                "No renderable PDF pages were found for this submission.", ct);
            return;
        }

        // ── Analyze ───────────────────────────────────────────────────────────
        await SetStageAsync(job, "Analyzing", ct);

        var emailBody = await _db.Set<InboundEmail>()
            .Where(e => e.LinkedSubmissionId == job.SubmissionId)
            .Select(e => e.BodyText)
            .FirstOrDefaultAsync(ct);

        var analysis = await _analyzer.AnalyzeSubmissionAsync(pages, emailBody, ct);
        if (analysis == null)
        {
            await CompleteAsync(job, IntakeJobStatus.Failed,
                "Document analysis did not return a result.", ct);
            return;
        }

        // ── Persist ───────────────────────────────────────────────────────────
        // Phase 1 persists detected lines of business + the raw analysis. Deliverable
        // filing, the completeness check, and the account summary are added as later stages.
        await SetStageAsync(job, "Persisting", ct);

        var submission = await _db.Set<Submission>().FirstOrDefaultAsync(s => s.Id == job.SubmissionId, ct);
        if (submission != null)
        {
            var lobs = analysis.Boundaries.Select(b => b.LineOfBusiness)
                .Concat(analysis.PerLob.Select(p => p.LineOfBusiness))
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (lobs.Count > 0)
                submission.LinesOfBusiness = JsonSerializer.Serialize(lobs);
            if (!string.IsNullOrWhiteSpace(analysis.QuotingLineOfBusiness))
                submission.QuotingLineOfBusiness = analysis.QuotingLineOfBusiness.Trim();
        }

        job.ResultJson = JsonSerializer.Serialize(analysis);
        var lowConfidence = string.Equals(analysis.Confidence, "Low", StringComparison.OrdinalIgnoreCase);
        await CompleteAsync(job,
            lowConfidence || analysis.Boundaries.Count == 0 ? IntakeJobStatus.NeedsReview : IntakeJobStatus.Completed,
            null, ct);

        _logger.LogInformation("Intake job {JobId} finished with status {Status}.", job.Id, job.Status);
    }

    private async Task SetStageAsync(IntakeJob job, string stage, CancellationToken ct)
    {
        job.Stage = stage;
        await _db.SaveChangesAsync(ct);
    }

    private async Task CompleteAsync(IntakeJob job, IntakeJobStatus status, string? error, CancellationToken ct)
    {
        job.Status = status;
        job.ErrorMessage = error is null ? null : Truncate(error, 2000);
        job.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsPdf(Attachment a) =>
        (a.ContentType?.Contains("pdf", StringComparison.OrdinalIgnoreCase) ?? false)
        || a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
