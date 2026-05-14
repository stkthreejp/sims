using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SIMS.Application.Common;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Application.Services;

public class DocumentMergeService : IDocumentMergeService
{
    private static readonly Regex RepeatBlockRegex = new(@"\{\{#(?<block>[A-Za-z0-9_.]+)\}\}(?<body>.*?)\{\{/(?<close>[A-Za-z0-9_.]+)\}\}", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex TagRegex = new(@"\{\{(?<tag>[A-Za-z0-9_.]+)(?:\s*\|\s*(?<format>[^}]+?))?\}\}", RegexOptions.Compiled);

    public string MergeText(string template, DocumentMergeData data)
        => Merge(template, data, WebUtility.HtmlEncode);

    public string MergeHtml(string template, DocumentMergeData data)
        => Merge(template, data, value => value);

    public byte[] MergeDocx(byte[] bytes, DocumentMergeData data)
    {
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using (var source = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true))
        using (var target = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in source.Entries)
            {
                var copy = target.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var sourceStream = entry.Open();
                using var targetStream = copy.Open();

                if (IsWordXmlPart(entry.FullName))
                {
                    using var reader = new StreamReader(sourceStream, Encoding.UTF8);
                    var xml = reader.ReadToEnd();
                    var merged = Merge(xml, data, WebUtility.HtmlEncode);
                    using var writer = new StreamWriter(targetStream, new UTF8Encoding(false));
                    writer.Write(merged);
                }
                else
                {
                    sourceStream.CopyTo(targetStream);
                }
            }
        }

        return output.ToArray();
    }

    public string FormatValue(object? value, string? format = null)
    {
        if (value == null)
            return string.Empty;

        if (value is DateOnly date)
            return date.ToString(string.IsNullOrWhiteSpace(format) ? "MM/dd/yyyy" : format, CultureInfo.InvariantCulture);
        if (value is DateTime dateTime)
            return dateTime.ToString(string.IsNullOrWhiteSpace(format) ? "MM/dd/yyyy" : format, CultureInfo.InvariantCulture);
        if (value is decimal decimalValue)
            return FormatDecimal(decimalValue, format);
        if (value is double doubleValue)
            return FormatDecimal((decimal)doubleValue, format);
        if (value is float floatValue)
            return FormatDecimal((decimal)floatValue, format);
        if (value is int or long or short)
            return FormatNumber(value, format);
        if (value is bool boolValue)
            return boolValue ? "Yes" : "No";

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private string Merge(string template, DocumentMergeData data, Func<string, string> encode)
    {
        var withRepeats = RepeatBlockRegex.Replace(template, match =>
        {
            var blockName = match.Groups["block"].Value;
            var closeName = match.Groups["close"].Value;
            if (!string.Equals(blockName, closeName, StringComparison.OrdinalIgnoreCase))
                return match.Value;
            if (!data.RepeatingValues.TryGetValue(blockName, out var rows) || rows.Count == 0)
                return string.Empty;

            var body = match.Groups["body"].Value;
            var mergedRows = rows.Select(row =>
            {
                var rowData = new DocumentMergeData();
                foreach (var (key, value) in data.Values)
                    rowData.Values[key] = value;
                foreach (var (key, value) in row)
                    rowData.Values[key] = value;

                return ReplaceSimpleTags(body, rowData, encode);
            });

            return string.Concat(mergedRows);
        });

        return ReplaceSimpleTags(withRepeats, data, encode);
    }

    private string ReplaceSimpleTags(string template, DocumentMergeData data, Func<string, string> encode)
    {
        return TagRegex.Replace(template, match =>
        {
            var tag = match.Groups["tag"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value.Trim() : null;
            if (!data.Values.TryGetValue(tag, out var value))
                return string.Empty;

            return encode(FormatValue(value, format));
        });
    }

    private static bool IsWordXmlPart(string entryName)
        => entryName.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
            && entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static string FormatDecimal(decimal value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return value.ToString("0.##", CultureInfo.InvariantCulture);

        return format.Trim().ToLowerInvariant() switch
        {
            "currency" => value.ToString("C2", CultureInfo.GetCultureInfo("en-US")),
            "number" => value.ToString("N2", CultureInfo.GetCultureInfo("en-US")),
            "percent" => value.ToString("P2", CultureInfo.GetCultureInfo("en-US")),
            _ => value.ToString(format, CultureInfo.InvariantCulture),
        };
    }

    private static string FormatNumber(object value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        if (format.Equals("number", StringComparison.OrdinalIgnoreCase))
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.GetCultureInfo("en-US"));

        return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(format, CultureInfo.InvariantCulture);
    }
}
