using SIMS.Application.DTOs.DocumentExtraction;

namespace SIMS.Application.Interfaces.Services;

/// <summary>
/// One-pass Claude vision analysis of a submission's rendered pages. Returns the
/// page-span → (form, LOB) boundary map, the monoline quoting line, and per-LOB field
/// extraction. Used by the intake worker. Returns null if the model call fails, refuses,
/// or the response can't be parsed — the caller marks the intake job Failed and leaves
/// the submission untouched.
/// </summary>
public interface ISubmissionIntakeAnalyzer
{
    Task<SubmissionAnalysis?> AnalyzeSubmissionAsync(
        IReadOnlyList<RenderedPage> pages,
        string? emailBodyContext,
        CancellationToken ct = default);
}
