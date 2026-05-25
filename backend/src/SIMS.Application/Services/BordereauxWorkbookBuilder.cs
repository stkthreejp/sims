using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SIMS.Application.DTOs.Bordereaux;

namespace SIMS.Application.Services;

internal static class BordereauxWorkbookBuilder
{
    public static byte[] BuildLondonBordereaux(IReadOnlyList<BordereauxPremiumPreviewRowDto> rows, IReadOnlyList<string> requiredTabs)
    {
        var sheetNames = requiredTabs.Count > 0
            ? requiredTabs
            : new[] { "Premium Bordereaux" };

        var sheets = sheetNames
            .Select(name => new WorksheetData(name, IsPremiumSheet(name) ? BuildPremiumRows(rows) : BuildEmptyRows(name)))
            .ToList();

        return BuildWorkbook(sheets);
    }

    public static byte[] BuildAccountCurrent(IReadOnlyList<BordereauxPremiumPreviewRowDto> rows)
    {
        var totalGross = rows.Sum(r => r.GrossPremium);
        var totalCommission = rows.Sum(r => r.GrossCommission);
        var totalFees = rows.Sum(r => r.Fees);
        var totalNet = rows.Sum(r => r.NetDueCarrier);
        var data = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Account Current" },
            new object?[] { "Rows", rows.Count },
            new object?[] { "Gross Premium", totalGross },
            new object?[] { "Gross Commission", totalCommission },
            new object?[] { "Fees", totalFees },
            new object?[] { "Net Due Carrier", totalNet },
            Array.Empty<object?>(),
            new object?[] { "Policy Number", "Transaction", "Insured", "Gross Premium", "Gross Commission", "Fees", "Net Due Carrier" },
        };

        data.AddRange(rows.Select(row => new object?[]
        {
            row.PolicyNumber,
            row.TransactionType.ToString(),
            row.InsuredName,
            row.GrossPremium,
            row.GrossCommission,
            row.Fees,
            row.NetDueCarrier,
        }));

        return BuildWorkbook(new[] { new WorksheetData("Account Current", data) });
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildPremiumRows(IReadOnlyList<BordereauxPremiumPreviewRowDto> rows)
    {
        var data = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "Reporting Date",
                "Policy Number",
                "Transaction Number",
                "Transaction Type",
                "Insured",
                "State",
                "Gross Premium",
                "Gross Commission",
                "Fees",
                "Net Due Carrier",
                "Invoice Number",
            },
        };

        data.AddRange(rows.Select(row => new object?[]
        {
            row.ReportingDate.ToString("yyyy-MM-dd"),
            row.PolicyNumber,
            row.TransactionNumber,
            row.TransactionType.ToString(),
            row.InsuredName,
            row.InsuredState,
            row.GrossPremium,
            row.GrossCommission,
            row.Fees,
            row.NetDueCarrier,
            row.InvoiceNumber,
        }));

        return data;
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildEmptyRows(string sheetName)
        => new[]
        {
            new object?[] { "Tab", sheetName },
            new object?[] { "Status", "No detail rows generated in this foundation export." },
        };

    private static bool IsPremiumSheet(string sheetName)
        => sheetName.Contains("premium", StringComparison.OrdinalIgnoreCase)
            || sheetName.Contains("general liability", StringComparison.OrdinalIgnoreCase)
            || sheetName.Contains("inland marine", StringComparison.OrdinalIgnoreCase);

    private static byte[] BuildWorkbook(IReadOnlyList<WorksheetData> sheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml(sheets.Count));
            WriteEntry(archive, "_rels/.rels", PackageRelationshipsXml());
            WriteEntry(archive, "xl/workbook.xml", WorkbookXml(sheets));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationshipsXml(sheets.Count));

            for (var i = 0; i < sheets.Count; i++)
                WriteEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", WorksheetXml(sheets[i].Rows));
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string ContentTypesXml(int sheetCount)
    {
        var overrides = new StringBuilder();
        for (var i = 1; i <= sheetCount; i++)
            overrides.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");

        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>{overrides}</Types>""";
    }

    private static string PackageRelationshipsXml()
        => """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private static string WorkbookXml(IReadOnlyList<WorksheetData> sheets)
    {
        var sheetXml = string.Join("", sheets.Select((sheet, i) =>
            $"""<sheet name="{XmlText(SafeSheetName(sheet.Name))}" sheetId="{i + 1}" r:id="rId{i + 1}"/>"""));
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>{sheetXml}</sheets></workbook>""";
    }

    private static string WorkbookRelationshipsXml(int sheetCount)
    {
        var relationships = new StringBuilder();
        for (var i = 1; i <= sheetCount; i++)
            relationships.Append($"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">{relationships}</Relationships>""";
    }

    private static string WorksheetXml(IReadOnlyList<IReadOnlyList<object?>> rows)
    {
        var body = new StringBuilder();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            body.Append($"""<row r="{rowIndex + 1}">""");
            for (var colIndex = 0; colIndex < rows[rowIndex].Count; colIndex++)
                body.Append(CellXml(rowIndex + 1, colIndex + 1, rows[rowIndex][colIndex]));
            body.Append("</row>");
        }

        return $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>{body}</sheetData></worksheet>""";
    }

    private static string CellXml(int row, int col, object? value)
    {
        var reference = $"{ColumnName(col)}{row}";
        return value switch
        {
            null => $"""<c r="{reference}"/>""",
            decimal d => $"""<c r="{reference}"><v>{d.ToString(CultureInfo.InvariantCulture)}</v></c>""",
            int i => $"""<c r="{reference}"><v>{i.ToString(CultureInfo.InvariantCulture)}</v></c>""",
            _ => $"""<c r="{reference}" t="inlineStr"><is><t>{XmlText(value.ToString() ?? string.Empty)}</t></is></c>""",
        };
    }

    private static string ColumnName(int number)
    {
        var name = string.Empty;
        while (number > 0)
        {
            var modulo = (number - 1) % 26;
            name = Convert.ToChar('A' + modulo) + name;
            number = (number - modulo) / 26;
        }
        return name;
    }

    private static string XmlText(string value) => new XText(value).ToString();

    private static string SafeSheetName(string value)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var cleaned = invalid.Aggregate(value, (current, c) => current.Replace(c, '-')).Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "Sheet";
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private sealed record WorksheetData(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows);
}
