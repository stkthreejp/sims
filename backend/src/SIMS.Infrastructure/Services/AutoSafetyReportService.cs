using SIMS.Application.Common;
using SIMS.Application.DTOs.Attachments;
using SIMS.Application.DTOs.Quotes;
using SIMS.Application.Interfaces.Services;
using SIMS.Domain.Entities;
using SIMS.Domain.Enums;
using SIMS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

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
            pdfBytes = BuildPdf(quote, safetyResult.Value);
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

    private static byte[] BuildPdf(Quote quote, AutoSafetySummaryDto safety)
    {
        using var document = new PdfDocument();
        document.PageSettings.Margins.All = 34;
        document.PageSettings.Size = PdfPageSize.Letter;

        var writer = new PdfReportWriter(document);
        writer.Title("Auto Safety Snapshot", $"Point-in-time underwriting report generated {DateTime.UtcNow:MM/dd/yyyy HH:mm} UTC.");

        var insured = quote.Submission.Insured;
        writer.MetricGrid([
            ("Carrier", safety.CarrierName ?? insured.DisplayName),
            ("USDOT", safety.UsDotNumber ?? "-"),
            ("Quote", quote.QuoteNumber),
            ("Risk", safety.OverallRiskLevel),
            ("Insured", insured.DisplayName),
            ("Power Units", safety.PowerUnits?.ToString("N0") ?? "-"),
            ("Drivers", safety.DriverCount?.ToString("N0") ?? "-"),
            ("SIMS ISS", safety.Iss.Score == null ? (safety.Iss.Label ?? "Pending") : $"{safety.Iss.Label ?? safety.Iss.Status} {safety.Iss.Score}"),
        ]);

        writer.Section("Summary");
        writer.Paragraph($"Snapshot: {safety.SnapshotMonth ?? "-"}    Methodology: {safety.MethodologyVersion ?? "-"}    Data refreshed: {FormatDateTime(safety.DataRefreshedAt)}");
        writer.Paragraph($"ISS basis: {safety.Iss.Basis}    Source: {safety.Iss.Source}");
        if (!string.IsNullOrWhiteSpace(safety.Iss.Explanation))
            writer.Paragraph(safety.Iss.Explanation);
        if (safety.SummaryFlags.Count > 0)
            writer.Paragraph($"Flags: {string.Join("; ", safety.SummaryFlags)}");

        AppendOos(writer, safety.Oos);
        AppendAccidents(writer, safety.AccidentSummary);
        AppendBasics(writer, safety.Basics);
        AppendRadius(writer, safety.RadiusSummary, safety.GeographicHotspots);
        AppendTrend(writer, "Inspection History", safety.InspectionTrend);
        AppendTrend(writer, "Violation History", safety.ViolationTrend);
        AppendEvents(writer, safety.RecentSevereEvents);

        writer.Section("Notes");
        writer.Paragraph("This SIMS report preserves the auto safety view available at generation time. Official SMS percentiles, SIMS peer percentiles, ISS estimates, geocoded inspection locations, and radius bands depend on the FMCSA source data and imported analytics available when the report was created.");

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void AppendOos(PdfReportWriter writer, AutoSafetyOosDto oos)
    {
        writer.Section("SAFER / OOS");
        writer.Table(
            ["Category", "Rate", "OOS / Inspections", "National Avg"],
            [
                ["Overall", FormatPct(oos.OverallOosRate), $"{oos.OverallOosCount} / {oos.InspectionCount}", FormatPct(oos.OverallNationalAverageRate)],
                ["Driver", FormatPct(oos.DriverOosRate), $"{oos.DriverOosCount} / {oos.DriverInspectionCount}", FormatPct(oos.DriverNationalAverageRate)],
                ["Vehicle", FormatPct(oos.VehicleOosRate), $"{oos.VehicleOosCount} / {oos.VehicleInspectionCount}", FormatPct(oos.VehicleNationalAverageRate)],
                ["Hazmat", FormatPct(oos.HazmatOosRate), $"{oos.HazmatOosCount} / {oos.HazmatInspectionCount}", FormatPct(oos.HazmatNationalAverageRate)],
            ]);
    }

    private static void AppendAccidents(PdfReportWriter writer, AutoSafetyAccidentSummaryDto accident)
    {
        writer.Section("Accident Summary");
        writer.Table(
            ["Fatal", "Injury", "Tow", "Reportable", "Ratio"],
            [[accident.FatalCount.ToString(), accident.InjuryCount.ToString(), accident.TowCount.ToString(), accident.TotalReportableCount.ToString(), FormatPct(accident.AccidentToPowerUnitRatio)]]);
    }

    private static void AppendBasics(PdfReportWriter writer, IReadOnlyCollection<AutoSafetyBasicDto> basics)
    {
        writer.Section("CSA / BASICs");
        writer.Table(
            ["BASIC", "Source", "Measure", "Score", "Events", "OOS", "12 Month"],
            basics.Select(b => new[]
            {
                b.Basic,
                b.ScoreSource,
                FormatDecimal(b.Measure),
                b.Percentile == null ? (b.IsPrioritized ? "Alert" : "-") : $"{b.Percentile:0}%",
                b.EventCount.ToString(),
                b.OutOfServiceCount.ToString(),
                $"{b.RecentEventCount} events / {b.RecentOutOfServiceCount} OOS",
            }).ToList());
    }

    private static void AppendRadius(PdfReportWriter writer, AutoSafetyRadiusSummaryDto radius, IReadOnlyCollection<AutoSafetyHotspotDto> hotspots)
    {
        writer.Section("Radius Of Operations");
        if (!string.IsNullOrWhiteSpace(radius.Note))
            writer.Paragraph($"{radius.Precision}: {radius.Note}");

        writer.Table(
            ["Band", "Inspections", "OOS"],
            radius.Bands.Select(b => new[] { b.Label, b.InspectionCount.ToString(), b.OutOfServiceCount.ToString() }).ToList());

        if (radius.MapPoints.Count > 0)
        {
            writer.Subsection("Top Map Points");
            writer.Table(
                ["Location", "Precision", "Inspections", "OOS"],
                radius.MapPoints.Take(10).Select(p => new[] { p.Label, p.Precision, p.InspectionCount.ToString(), p.OutOfServiceCount.ToString() }).ToList());
        }
        else if (hotspots.Count > 0)
        {
            writer.Subsection("Hotspots");
            writer.Table(
                ["State", "Inspections", "Violations", "OOS"],
                hotspots.Select(h => new[] { h.State, h.InspectionCount.ToString(), h.ViolationCount.ToString(), h.OutOfServiceCount.ToString() }).ToList());
        }
    }

    private static void AppendTrend(PdfReportWriter writer, string title, IReadOnlyCollection<AutoSafetyTrendBucketDto> buckets)
    {
        writer.Section(title);
        writer.Table(
            ["Months Ago", "Total", "OOS", "OOS Rate"],
            buckets.Select(b => new[] { b.Label, b.TotalCount.ToString(), b.OutOfServiceCount.ToString(), FormatPct(b.OutOfServiceRate) }).ToList());
    }

    private static void AppendEvents(PdfReportWriter writer, IReadOnlyCollection<AutoSafetyEventDto> events)
    {
        writer.Section("Recent Severe Events");
        if (events.Count == 0)
        {
            writer.Paragraph("No recent high-severity or OOS events in the imported window.");
            return;
        }

        writer.Table(
            ["Date", "Type", "Description", "State", "BASIC"],
            events.Take(20).Select(e => new[] { e.Date.ToString("MM/dd/yyyy"), e.EventType, e.Description, e.State ?? "-", e.Basic ?? "-" }).ToList());
    }

    private static string FormatPct(decimal? value) => value == null ? "-" : $"{value:0.##}%";
    private static string FormatDecimal(decimal? value) => value == null ? "-" : $"{value:0.##}";
    private static string FormatDateTime(DateTime? value) => value == null ? "-" : value.Value.ToString("MM/dd/yyyy HH:mm 'UTC'");
    private static string SanitizeFileName(string value) => string.Join("_", value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");

    private sealed class PdfReportWriter
    {
        private readonly PdfDocument _document;
        private readonly PdfFont _titleFont = new PdfStandardFont(PdfFontFamily.Helvetica, 18, PdfFontStyle.Bold);
        private readonly PdfFont _sectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
        private readonly PdfFont _subsectionFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
        private readonly PdfFont _labelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 7, PdfFontStyle.Bold);
        private readonly PdfFont _bodyFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f);
        private readonly PdfFont _bodyBoldFont = new PdfStandardFont(PdfFontFamily.Helvetica, 8.5f, PdfFontStyle.Bold);
        private readonly PdfBrush _ink = new PdfSolidBrush(new PdfColor(23, 32, 51));
        private readonly PdfBrush _muted = new PdfSolidBrush(new PdfColor(100, 116, 139));
        private readonly PdfBrush _headerFill = new PdfSolidBrush(new PdfColor(238, 242, 247));
        private readonly PdfPen _border = new(new PdfColor(203, 213, 225), 0.7f);
        private PdfPage _page;
        private float _y;

        public PdfReportWriter(PdfDocument document)
        {
            _document = document;
            _page = _document.Pages.Add();
            _y = 0;
        }

        private PdfGraphics Graphics => _page.Graphics;
        private float Width => _page.GetClientSize().Width;
        private float Height => _page.GetClientSize().Height;

        public void Title(string title, string subtitle)
        {
            DrawText(title, _titleFont, _ink, 24);
            DrawText(subtitle, _bodyFont, _muted, 14);
            _y += 6;
        }

        public void Section(string title)
        {
            EnsureSpace(30);
            _y += 6;
            DrawText(title, _sectionFont, _ink, 18);
            Graphics.DrawLine(_border, 0, _y, Width, _y);
            _y += 6;
        }

        public void Subsection(string title)
        {
            EnsureSpace(20);
            _y += 5;
            DrawText(title, _subsectionFont, _ink, 15);
        }

        public void Paragraph(string text)
        {
            var lines = Wrap(text, 116).ToList();
            EnsureSpace(lines.Count * 12 + 4);
            foreach (var line in lines)
                DrawText(line, _bodyFont, _muted, 12);
            _y += 2;
        }

        public void MetricGrid(IReadOnlyList<(string Label, string Value)> metrics)
        {
            const int columns = 4;
            const float gap = 6;
            const float cellHeight = 44;
            var cellWidth = (Width - gap * (columns - 1)) / columns;

            for (var i = 0; i < metrics.Count; i += columns)
            {
                EnsureSpace(cellHeight + 6);
                for (var col = 0; col < columns && i + col < metrics.Count; col++)
                {
                    var metric = metrics[i + col];
                    var x = col * (cellWidth + gap);
                    Graphics.DrawRectangle(_border, new RectangleF(x, _y, cellWidth, cellHeight));
                    Graphics.DrawString(metric.Label.ToUpperInvariant(), _labelFont, _muted, new RectangleF(x + 7, _y + 7, cellWidth - 14, 10));
                    Graphics.DrawString(Truncate(metric.Value, 36), _bodyBoldFont, _ink, new RectangleF(x + 7, _y + 22, cellWidth - 14, 14));
                }
                _y += cellHeight + 6;
            }
        }

        public void Table(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (headers.Count == 0)
                return;

            var widths = BuildColumnWidths(headers.Count);
            DrawHeader(headers, widths);

            if (rows.Count == 0)
            {
                DrawRow(Enumerable.Repeat("-", headers.Count).ToList(), widths);
                return;
            }

            foreach (var row in rows)
                DrawRow(row, widths);

            _y += 4;
        }

        private void DrawHeader(IReadOnlyList<string> headers, IReadOnlyList<float> widths)
        {
            EnsureSpace(19);
            var x = 0f;
            for (var i = 0; i < headers.Count; i++)
            {
                Graphics.DrawRectangle(_border, _headerFill, new RectangleF(x, _y, widths[i], 18));
                Graphics.DrawString(Truncate(headers[i].ToUpperInvariant(), 24), _labelFont, _ink, new RectangleF(x + 4, _y + 5, widths[i] - 8, 10));
                x += widths[i];
            }
            _y += 18;
        }

        private void DrawRow(IReadOnlyList<string> row, IReadOnlyList<float> widths)
        {
            const float rowHeight = 20;
            EnsureSpace(rowHeight);
            var x = 0f;
            for (var i = 0; i < widths.Count; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                Graphics.DrawRectangle(_border, new RectangleF(x, _y, widths[i], rowHeight));
                Graphics.DrawString(Truncate(value, ColumnLimit(widths[i])), _bodyFont, _ink, new RectangleF(x + 4, _y + 5, widths[i] - 8, 10));
                x += widths[i];
            }
            _y += rowHeight;
        }

        private IReadOnlyList<float> BuildColumnWidths(int count)
        {
            if (count == 7) return [Width * .22f, Width * .15f, Width * .11f, Width * .10f, Width * .10f, Width * .08f, Width * .24f];
            if (count == 5) return [Width * .12f, Width * .16f, Width * .44f, Width * .10f, Width * .18f];
            if (count == 4) return [Width * .25f, Width * .25f, Width * .25f, Width * .25f];
            if (count == 3) return [Width * .45f, Width * .275f, Width * .275f];
            return Enumerable.Repeat(Width / count, count).ToList();
        }

        private void DrawText(string text, PdfFont font, PdfBrush brush, float lineHeight)
        {
            Graphics.DrawString(text, font, brush, new RectangleF(0, _y, Width, lineHeight));
            _y += lineHeight;
        }

        private void EnsureSpace(float requiredHeight)
        {
            if (_y + requiredHeight <= Height)
                return;

            _page = _document.Pages.Add();
            _y = 0;
        }

        private static IEnumerable<string> Wrap(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
                yield break;

            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = string.Empty;
            foreach (var word in words)
            {
                if ((line.Length + word.Length + 1) > maxChars)
                {
                    yield return line;
                    line = word;
                }
                else
                {
                    line = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
                }
            }

            if (!string.IsNullOrWhiteSpace(line))
                yield return line;
        }

        private static int ColumnLimit(float width) => Math.Max(8, (int)(width / 4.2f));
        private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : $"{value[..Math.Max(0, maxLength - 1)]}...";
    }
}
