namespace SIMS.Application.DTOs.DocumentExtraction;

/// <summary>A rendered page of the combined submission PDF, ready for Claude vision.</summary>
public record RenderedPage(int PageNumber, byte[] PngBytes);

/// <summary>
/// A contiguous page span the analyzer identified as one ACORD form / document for one
/// line of business — used to split the combined PDF into separate filed attachments.
/// </summary>
public class FormSpan
{
    public int StartPage { get; set; }                 // 1-indexed, inclusive
    public int EndPage { get; set; }                   // 1-indexed, inclusive
    public string Form { get; set; } = string.Empty;   // e.g. "Acord125","Acord126","Acord127","Acord146","LossRun","ScheduleOfValues","SignedApplication","Other"
    public string? LineOfBusiness { get; set; }        // "GeneralLiability" | "InlandMarine" | "AutoLiability" | "AutoPhysicalDamage" | null
}

/// <summary>
/// Result of the one-pass Claude vision analysis of a submission's combined PDF: the
/// page-span → (form, LOB) map, the monoline quoting line, and per-LOB field extraction.
/// </summary>
public class SubmissionAnalysis
{
    public List<FormSpan> Boundaries { get; set; } = [];
    public string? QuotingLineOfBusiness { get; set; }
    public List<DocumentLobExtraction> PerLob { get; set; } = [];
    public string? Confidence { get; set; }            // "High" | "Medium" | "Low"
    public string? Rationale { get; set; }
}
