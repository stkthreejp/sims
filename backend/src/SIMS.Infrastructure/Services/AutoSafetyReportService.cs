using System.Net;
using System.Text;
using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;

namespace SIMS.Infrastructure.Services;

public class AutoSafetyReportService : IAutoSafetyReportService
{
    private readonly ApplicationDbContext _db;
    private readonly IFmcsaSafetyService _safety;
    private readonly IAttachmentService _attachments;

    public AutoSafetyReportService(
        ApplicationDbContext db,
        IFmcsaSafetyService safety,
        IAttachmentService attachments)
    {
        _db = db;
        _safety = safety;
        _attachments = attachments;
    }

    public async Task<Result<AttachmentDto>> GenerateQuoteReportAsync(Guid quoteId, Guid userId, CancellationToken ct = default)
    {
        var quote = await _db.Set<Quote>()
            .AsNoTracking()
            .Include(q => q.Carrier)
            .Include(q => q.Submission).ThenInclude(s => s.Insured)
            .FirstOrDefaultAsync(q => q.Id == quoteId, ct);

        if (quote == null)
            return Result<AttachmentDto>.Failure("QUOTE_NOT_FOUND", "Quote was not found.");

        var safetyResult = await _safety.GetQuoteAutoSafetyAsync(quoteId, ct);
        if (!safetyResult.IsSuccess || safetyResult.Value == null)
            return Result<AttachmentDto>.Failure(safetyResult.ErrorCode ?? "AUTO_SAFETY_UNAVAILABLE", safetyResult.ErrorMessage ?? "Auto safety data is not available.");

        if (safetyResult.Value.Status != "Ready")
            return Result<AttachmentDto>.Failure("AUTO_SAFETY_NOT_READY", safetyResult.Value.Message ?? "Auto safety data is not ready yet.");

        byte[] pdfBytes;
        try
        {
            pdfBytes = ConvertHtmlToPdf(BuildHtml(quote, safetyResult.Value));
        }
        catch (Exception ex)
        {
            return Result<AttachmentDto>.Failure("AUTO_SAFETY_REPORT_FAILED", $"Auto safety report could not be created: {ex.Message}");
        }

        var carrierName = SanitizeFileName(safetyResult.Value.CarrierName ?? quote.Submission.Insured.DisplayName);
        var fileName = $"AutoSafety_{carrierName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        await using var stream = new MemoryStream(pdfBytes);
        return await _attachments.CreateGeneratedAsync(
            DocumentEntityType.Policy,
            quoteId,
            stream,
            fileName,
            "application/pdf",
            pdfBytes.LongLength,
            DocumentType.UnderwritingMemo,
            $"Auto safety snapshot generated {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.",
            userId);
    }

    private static string BuildHtml(Quote quote, AutoSafetySummaryDto safety)
    {
        var insured = quote.Submission.Insured;
        var generatedAt = DateTime.UtcNow;
        var html = new StringBuilder();

        html.Append("""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
              <style>
                @page { margin: 0.55in; }
                body { font-family: Arial, sans-serif; color: #172033; font-size: 9.5pt; line-height: 1.35; }
                h1 { margin: 0; font-size: 18pt; }
                h2 { margin: 18px 0 8px; font-size: 12pt; color: #25344d; border-bottom: 1px solid #b8c2d2; padding-bottom: 4px; }
                h3 { margin: 0 0 4px; font-size: 10pt; color: #25344d; }
                p { margin: 0 0 6px; }
                .muted { color: #64748b; }
                .small { font-size: 8pt; }
                .grid { width: 100%; border-collapse: separate; border-spacing: 6px; margin-top: 10px; }
                .tile { border: 1px solid #cbd5e1; padding: 8px; vertical-align: top; }
                .label { color: #64748b; font-size: 7.5pt; font-weight: bold; text-transform: uppercase; }
                .value { font-size: 12pt; font-weight: bold; margin-top: 2px; }
                table { width: 100%; border-collapse: collapse; margin-top: 6px; }
                th, td { border: 1px solid #cbd5e1; padding: 5px 6px; text-align: left; vertical-align: top; }
                th { background: #eef2f7; color: #334155; font-size: 8pt; text-transform: uppercase; }
                .status { display: inline-block; padding: 3px 6px; border-radius: 4px; font-weight: bold; }
                .green { color: #047857; background: #ecfdf5; border: 1px solid #a7f3d0; }
                .yellow { color: #b45309; background: #fffbeb; border: 1px solid #fde68a; }
                .red { color: #b91c1c; background: #fef2f2; border: 1px solid #fecaca; }
                .gray { color: #475569; background: #f8fafc; border: 1px solid #cbd5e1; }
              </style>
            </head>
            <body>
            """);

        html.Append("<h1>Auto Safety Snapshot</h1>");
        html.Append("<p class=\"muted\">Point-in-time underwriting report generated ")
            .Append(Html(generatedAt.ToString("MM/dd/yyyy HH:mm 'UTC'")))
            .Append(".</p>");

        html.Append("<table class=\"grid\"><tr>");
        AppendTile(html, "Carrier", safety.CarrierName ?? insured.DisplayName);
        AppendTile(html, "USDOT", safety.UsDotNumber ?? "-");
        AppendTile(html, "Quote", quote.QuoteNumber);
        AppendTile(html, "Risk", safety.OverallRiskLevel);
        html.Append("</tr><tr>");
        AppendTile(html, "Insured", insured.DisplayName);
        AppendTile(html, "Power Units", safety.PowerUnits?.ToString("N0") ?? "-");
        AppendTile(html, "Drivers", safety.DriverCount?.ToString("N0") ?? "-");
        AppendTile(html, "SIMS ISS", safety.Iss.Score == null ? (safety.Iss.Label ?? "Pending") : $"{safety.Iss.Label ?? safety.Iss.Status} {safety.Iss.Score}");
        html.Append("</tr></table>");

        html.Append("<h2>Summary</h2>");
        html.Append("<p><strong>Snapshot:</strong> ").Append(Html(safety.SnapshotMonth ?? "-"))
            .Append(" &nbsp; <strong>Methodology:</strong> ").Append(Html(safety.MethodologyVersion ?? "-"))
            .Append(" &nbsp; <strong>Data refreshed:</strong> ").Append(Html(FormatDateTime(safety.DataRefreshedAt)))
            .Append("</p>");
        if (safety.SummaryFlags.Count > 0)
            html.Append("<p><strong>Flags:</strong> ").Append(Html(string.Join("; ", safety.SummaryFlags))).Append("</p>");
        html.Append("<p><strong>ISS basis:</strong> ").Append(Html(safety.Iss.Basis))
            .Append(" &nbsp; <strong>Source:</strong> ").Append(Html(safety.Iss.Source))
            .Append("</p>");
        if (!string.IsNullOrWhiteSpace(safety.Iss.Explanation))
            html.Append("<p class=\"muted\">").Append(Html(safety.Iss.Explanation)).Append("</p>");

        AppendOosTable(html, safety.Oos);
        AppendAccidentTable(html, safety.AccidentSummary);
        AppendBasicsTable(html, safety.Basics);
        AppendRadiusTable(html, safety.RadiusSummary, safety.GeographicHotspots);
        AppendTrendTable(html, "Inspection History", safety.InspectionTrend);
        AppendTrendTable(html, "Violation History", safety.ViolationTrend);
        AppendEventsTable(html, safety.RecentSevereEvents);

        html.Append("""
            <h2>Notes</h2>
            <p class="small muted">
              This SIMS report preserves the auto safety view available at generation time. Official SMS percentiles,
              SIMS peer percentiles, ISS estimates, geocoded inspection locations, and radius bands depend on the FMCSA
              source data and imported analytics available when the report was created.
            </p>
            </body></html>
            """);

        return html.ToString();
    }

    private static void AppendOosTable(StringBuilder html, AutoSafetyOosDto oos)
    {
        html.Append("<h2>SAFER / OOS</h2><table><tr><th>Category</th><th>Rate</th><th>OOS / Inspections</th><th>National Avg</th></tr>");
        AppendOosRow(html, "Overall", oos.OverallOosRate, oos.OverallOosCount, oos.InspectionCount, oos.OverallNationalAverageRate);
        AppendOosRow(html, "Driver", oos.DriverOosRate, oos.DriverOosCount, oos.DriverInspectionCount, oos.DriverNationalAverageRate);
        AppendOosRow(html, "Vehicle", oos.VehicleOosRate, oos.VehicleOosCount, oos.VehicleInspectionCount, oos.VehicleNationalAverageRate);
        AppendOosRow(html, "Hazmat", oos.HazmatOosRate, oos.HazmatOosCount, oos.HazmatInspectionCount, oos.HazmatNationalAverageRate);
        html.Append("</table>");
    }

    private static void AppendAccidentTable(StringBuilder html, AutoSafetyAccidentSummaryDto accident)
    {
        html.Append("<h2>Accident Summary</h2><table><tr><th>Fatal</th><th>Injury</th><th>Tow</th><th>Reportable</th><th>Ratio</th></tr><tr>");
        html.Append("<td>").Append(accident.FatalCount).Append("</td>");
        html.Append("<td>").Append(accident.InjuryCount).Append("</td>");
        html.Append("<td>").Append(accident.TowCount).Append("</td>");
        html.Append("<td>").Append(accident.TotalReportableCount).Append("</td>");
        html.Append("<td>").Append(FormatPct(accident.AccidentToPowerUnitRatio)).Append("</td>");
        html.Append("</tr></table>");
    }

    private static void AppendBasicsTable(StringBuilder html, IReadOnlyCollection<AutoSafetyBasicDto> basics)
    {
        html.Append("<h2>CSA / BASICs</h2><table><tr><th>BASIC</th><th>Source</th><th>Measure</th><th>Score</th><th>Events</th><th>OOS</th><th>12 Month</th></tr>");
        foreach (var basic in basics)
        {
            html.Append("<tr><td>").Append(Html(basic.Basic)).Append("</td>")
                .Append("<td>").Append(Html(basic.ScoreSource)).Append("</td>")
                .Append("<td>").Append(FormatDecimal(basic.Measure)).Append("</td>")
                .Append("<td>").Append(basic.Percentile == null ? (basic.IsPrioritized ? "Alert" : "-") : $"{basic.Percentile:0}%").Append("</td>")
                .Append("<td>").Append(basic.EventCount).Append("</td>")
                .Append("<td>").Append(basic.OutOfServiceCount).Append("</td>")
                .Append("<td>").Append(basic.RecentEventCount).Append(" events / ").Append(basic.RecentOutOfServiceCount).Append(" OOS</td></tr>");
        }
        html.Append("</table>");
    }

    private static void AppendRadiusTable(StringBuilder html, AutoSafetyRadiusSummaryDto radius, IReadOnlyCollection<AutoSafetyHotspotDto> hotspots)
    {
        html.Append("<h2>Radius Of Operations</h2>");
        if (!string.IsNullOrWhiteSpace(radius.Note))
            html.Append("<p class=\"muted\"><strong>").Append(Html(radius.Precision)).Append(":</strong> ").Append(Html(radius.Note)).Append("</p>");

        html.Append("<table><tr><th>Band</th><th>Inspections</th><th>OOS</th></tr>");
        foreach (var band in radius.Bands)
            html.Append("<tr><td>").Append(Html(band.Label)).Append("</td><td>").Append(band.InspectionCount).Append("</td><td>").Append(band.OutOfServiceCount).Append("</td></tr>");
        html.Append("</table>");

        if (radius.MapPoints.Count > 0)
        {
            html.Append("<h3>Top Map Points</h3><table><tr><th>Location</th><th>Precision</th><th>Inspections</th><th>OOS</th></tr>");
            foreach (var point in radius.MapPoints.Take(10))
                html.Append("<tr><td>").Append(Html(point.Label)).Append("</td><td>").Append(Html(point.Precision)).Append("</td><td>").Append(point.InspectionCount).Append("</td><td>").Append(point.OutOfServiceCount).Append("</td></tr>");
            html.Append("</table>");
        }
        else if (hotspots.Count > 0)
        {
            html.Append("<h3>Hotspots</h3><table><tr><th>State</th><th>Inspections</th><th>Violations</th><th>OOS</th></tr>");
            foreach (var hotspot in hotspots)
                html.Append("<tr><td>").Append(Html(hotspot.State)).Append("</td><td>").Append(hotspot.InspectionCount).Append("</td><td>").Append(hotspot.ViolationCount).Append("</td><td>").Append(hotspot.OutOfServiceCount).Append("</td></tr>");
            html.Append("</table>");
        }
    }

    private static void AppendTrendTable(StringBuilder html, string title, IReadOnlyCollection<AutoSafetyTrendBucketDto> buckets)
    {
        html.Append("<h2>").Append(Html(title)).Append("</h2><table><tr><th>Months Ago</th><th>Total</th><th>OOS</th><th>OOS Rate</th></tr>");
        foreach (var bucket in buckets)
            html.Append("<tr><td>").Append(Html(bucket.Label)).Append("</td><td>").Append(bucket.TotalCount).Append("</td><td>").Append(bucket.OutOfServiceCount).Append("</td><td>").Append(FormatPct(bucket.OutOfServiceRate)).Append("</td></tr>");
        html.Append("</table>");
    }

    private static void AppendEventsTable(StringBuilder html, IReadOnlyCollection<AutoSafetyEventDto> events)
    {
        html.Append("<h2>Recent Severe Events</h2>");
        if (events.Count == 0)
        {
            html.Append("<p class=\"muted\">No recent high-severity or OOS events in the imported window.</p>");
            return;
        }

        html.Append("<table><tr><th>Date</th><th>Type</th><th>Description</th><th>State</th><th>BASIC</th></tr>");
        foreach (var item in events.Take(20))
            html.Append("<tr><td>").Append(Html(item.Date.ToString("MM/dd/yyyy"))).Append("</td><td>").Append(Html(item.EventType)).Append("</td><td>").Append(Html(item.Description)).Append("</td><td>").Append(Html(item.State ?? "-")).Append("</td><td>").Append(Html(item.Basic ?? "-")).Append("</td></tr>");
        html.Append("</table>");
    }

    private static void AppendTile(StringBuilder html, string label, string value)
        => html.Append("<td class=\"tile\"><div class=\"label\">").Append(Html(label)).Append("</div><div class=\"value\">").Append(Html(value)).Append("</div></td>");

    private static void AppendOosRow(StringBuilder html, string label, decimal? rate, int oosCount, int inspectionCount, decimal? nationalAverage)
        => html.Append("<tr><td>").Append(Html(label)).Append("</td><td>").Append(FormatPct(rate)).Append("</td><td>").Append(oosCount).Append(" / ").Append(inspectionCount).Append("</td><td>").Append(FormatPct(nationalAverage)).Append("</td></tr>");

    private static byte[] ConvertHtmlToPdf(string html)
    {
        using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(html));
        using var wordDoc = new WordDocument(htmlStream, FormatType.Html);
        using var renderer = new DocIORenderer();
        using var pdfDoc = renderer.ConvertToPDF(wordDoc);
        using var pdfStream = new MemoryStream();
        pdfDoc.Save(pdfStream);
        return pdfStream.ToArray();
    }

    private static string FormatPct(decimal? value) => value == null ? "-" : $"{value:0.##}%";
    private static string FormatDecimal(decimal? value) => value == null ? "-" : $"{value:0.##}";
    private static string FormatDateTime(DateTime? value) => value == null ? "-" : value.Value.ToString("MM/dd/yyyy HH:mm 'UTC'");
    private static string Html(string value) => WebUtility.HtmlEncode(value);
    private static string SanitizeFileName(string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
}
