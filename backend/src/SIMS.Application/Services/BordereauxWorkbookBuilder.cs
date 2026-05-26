using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using SIMS.Application.DTOs.Bordereaux;

namespace SIMS.Application.Services;

internal static class BordereauxWorkbookBuilder
{
    public static byte[] BuildLondonBordereaux(IReadOnlyList<BordereauxLondonPremiumRow> rows, IReadOnlyList<string> requiredTabs)
    {
        var sheetNames = requiredTabs.Count > 0
            ? requiredTabs
            : new[] { "Premium Bordereaux" };

        var sheets = sheetNames
            .Select(name => new WorksheetData(name, BuildLondonSheetRows(name, rows)))
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

    private static IReadOnlyList<IReadOnlyList<object?>> BuildLondonSheetRows(string sheetName, IReadOnlyList<BordereauxLondonPremiumRow> rows)
    {
        if (sheetName.Equals("Auto Veh Info", StringComparison.OrdinalIgnoreCase))
            return BuildAutoVehicleRows(rows.SelectMany(row => row.AutoVehicles).ToList());
        if (sheetName.Equals("IM Unit Info", StringComparison.OrdinalIgnoreCase))
            return BuildInlandMarineUnitRows(rows.SelectMany(row => row.ImUnits).ToList());

        if (IsLondonSectionSheet(sheetName))
            return BuildLondonSectionRows(sheetName, rows.Where(row => RowBelongsOnSheet(row.Source.LineOfBusiness, sheetName)).ToList());

        return BuildEmptyRows(sheetName);
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildAutoVehicleRows(IReadOnlyList<BordereauxAutoVehicleDetail> vehicles)
    {
        var data = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Certificate Ref", "Number", "YearMade", "Make", "Model", "VIN", "Type", "ACV", "Deductible", "Premium", "Rate" },
        };

        data.AddRange(vehicles.Select(vehicle => new object?[]
        {
            vehicle.CertificateRef,
            vehicle.Number,
            vehicle.YearMade,
            vehicle.Make,
            vehicle.Model,
            vehicle.Vin,
            vehicle.Type,
            vehicle.ActualCashValue,
            vehicle.Deductible,
            vehicle.Premium,
            vehicle.Rate,
        }));

        return data;
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildInlandMarineUnitRows(IReadOnlyList<BordereauxInlandMarineUnitDetail> units)
    {
        var data = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Certificate Ref", "Number", "YearMade", "Make", "Model", "Serial", "Type", "ACV", "ACV Note", "Deductible", "Premium", "Rate", "TransType" },
        };

        data.AddRange(units.Select(unit => new object?[]
        {
            unit.CertificateRef,
            unit.Number,
            unit.YearMade,
            unit.Make,
            unit.Model,
            unit.Serial,
            unit.Type,
            unit.ActualCashValue,
            unit.ActualCashValueNote,
            unit.Deductible,
            unit.Premium,
            unit.Rate,
            unit.TransactionType,
        }));

        return data;
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildLondonSectionRows(string sheetName, IReadOnlyList<BordereauxLondonPremiumRow> rows)
    {
        var headers = LondonHeaders(sheetName);
        var data = new List<IReadOnlyList<object?>>
        {
            headers.Refs,
            headers.Fields,
        };

        data.AddRange(rows.Select(row => LondonSectionRow(sheetName, row)));

        return data;
    }

    private static IReadOnlyList<object?> LondonSectionRow(string sheetName, BordereauxLondonPremiumRow row)
    {
        var values = new List<object?>
        {
            string.Empty,
            row.CoverholderName,
            row.CoverholderPin,
            row.Umr,
            row.PeriodStart.ToString("MM/dd/yyyy"),
            row.PeriodEnd.ToString("MM/dd/yyyy"),
            row.SectionNumber,
            row.ClassOfBusiness,
            row.RiskCode,
            row.InsuranceType,
            row.YearOfAccount,
            row.Source.PolicyNumber,
            row.Source.InsuredName,
            row.Source.InsuredState,
            "USA",
            row.Source.TransactionEffectiveDate.ToString("MM/dd/yyyy"),
            row.Source.ExpirationDate?.ToString("MM/dd/yyyy"),
            "USA",
            row.Source.InsuredState,
            TransactionCode(row.Source.TransactionType, row.Source.GrossPremium),
            row.Source.ReportingDate.ToString("MM/dd/yyyy"),
            row.CurrencyCode,
            row.Source.GrossPremium,
        };

        if (sheetName.Contains("Inland Marine", StringComparison.OrdinalIgnoreCase))
            values.AddRange(new object?[] { null, row.Source.GrossPremium, row.CommissionRate, row.CommissionAmount, null, null, row.NetPremiumToLondon, row.CurrencyCode });
        else if (sheetName.Contains("Commercial Auto", StringComparison.OrdinalIgnoreCase))
            values.AddRange(new object?[] { row.Source.GrossPremium, row.CommissionRate, row.CommissionAmount, row.NetPremiumToLondon, row.CurrencyCode });
        else
            values.AddRange(new object?[] { row.Source.GrossPremium, row.CommissionRate, row.CommissionAmount, null, null, row.NetPremiumToLondon, row.CurrencyCode });

        return values;
    }

    private static IReadOnlyList<IReadOnlyList<object?>> BuildEmptyRows(string sheetName)
        => new[]
        {
            new object?[] { "Tab", sheetName },
            new object?[] { "Status", "No detail rows generated in this foundation export." },
        };

    private static bool IsLondonSectionSheet(string sheetName)
        => sheetName.Contains("premium", StringComparison.OrdinalIgnoreCase)
            || sheetName.Contains("general liability", StringComparison.OrdinalIgnoreCase)
            || sheetName.Contains("commercial auto", StringComparison.OrdinalIgnoreCase)
            || sheetName.Contains("inland marine", StringComparison.OrdinalIgnoreCase);

    private static bool RowBelongsOnSheet(SIMS.Domain.Enums.PolicyLineOfBusiness lineOfBusiness, string sheetName)
    {
        if (sheetName.Contains("general liability", StringComparison.OrdinalIgnoreCase))
            return lineOfBusiness == SIMS.Domain.Enums.PolicyLineOfBusiness.GeneralLiability;
        if (sheetName.Contains("commercial auto", StringComparison.OrdinalIgnoreCase))
            return lineOfBusiness is SIMS.Domain.Enums.PolicyLineOfBusiness.AutoLiability or SIMS.Domain.Enums.PolicyLineOfBusiness.AutoPhysicalDamage;
        if (sheetName.Contains("inland marine", StringComparison.OrdinalIgnoreCase))
            return lineOfBusiness == SIMS.Domain.Enums.PolicyLineOfBusiness.InlandMarine;
        return true;
    }

    private static LondonHeaderRows LondonHeaders(string sheetName)
    {
        if (sheetName.Contains("Commercial Auto", StringComparison.OrdinalIgnoreCase))
            return CommercialAutoHeaders;
        if (sheetName.Contains("Inland Marine", StringComparison.OrdinalIgnoreCase))
            return InlandMarineHeaders;
        return GeneralLiabilityHeaders;
    }

    private static string TransactionCode(SIMS.Domain.Enums.TransactionType transactionType, decimal grossPremium)
        => transactionType switch
        {
            SIMS.Domain.Enums.TransactionType.Endorsement => grossPremium < 0 ? "RP" : "AP",
            SIMS.Domain.Enums.TransactionType.Cancellation => "RP",
            SIMS.Domain.Enums.TransactionType.Reinstatement => "AP",
            _ => "OP",
        };

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

    private static readonly LondonHeaderRows GeneralLiabilityHeaders = new(
        new object?[] { "Ref", "CR0013", "CR0014", "CR0005", "CR0001", "CR0002", "CR0007", "CR0017", "CR0016", "CR0019", "CR0010", "CR0029", "CR0035", "CR0039", "CR0041", "CR0030", "CR0031", "CR0050", "CR0048", "CR0056", "CR0057", "CR0020", "CR0021", "CR0059", "CR0061", "CR0062", null, null, "CR0065", "CR0066", "CR0025", "CR0088", "CR0096", "CR0097", "CR0098", "CR0099", "CR0100", "CR0101", "CR0102", "CR0038", "CR0040", "CR0046", "CR0047", "CR0049", "CR0226", "CR0051", "CR0052", "CR0053", "CR0054", "CR0055", "CR0086", "CR0087", "CR0089", "CR0090", "CR0091", "CR0092", "CR0093", "CR0094", "CR0095", "CR0315", "CR1298", null, null, null, null, null },
        new object?[] { "Field", "Coverholder Name", "Coverholder PIN", "Unique Market Reference (UMR)", "Reporting Period Start Date", "Reporting Period (End Date)", "Section No", "Class of Business", "Risk Code", "Type of Insurance (Direct or Type or Reinsurance)", "Year of Account", "Certificate Ref", "Insured Full Name, Last Name or Company Name", "Insured Country Sub-division: State, Province, Territory, Canton etc.", "Insured Country (see code list)", "Risk Inception Date", "Risk Expiry Date", "Location of risk - Country", "Location of Risk - Country Sub-division: State, Province, Territory, Canton etc.", "Transaction Type - Original Premium etc.", "Effective Date of Transaction", "Original Currency", "Total gross written premium", "Gross premium paid this time", "Commission %", "Commission Amount", "Brokerage %", "Brokerage Amount", "Net Premium to London in original currency", "Settlement Currency", "US Classification", "State of Filing (see code list)", "Surplus Lines Broker Name", "Surplus Lines Broker Licence No ", "New Jersey SLA No", "Surplus Lines Broker Address", "Surplus Lines Broker State", "Surplus Lines Broker Zip Code", "Surplus Lines Broker Country", "Insured Address ", "Insured Postcode, Zip Code or Similar", "Location of Risk, Address", "Location of Risk, County", "Location of Risk, Postcode, zip code or similar", "Country of Registration", "Sum Insured Currency (see code list)", "Sum Insured Amount", "Aggregate Sum Insured Amount", "Deductible or Excess Amount", "Deductible or Excess Basis", "Other Fees or Deductions Description", "Other Fees or Deductions Amount", "Intermediary - Role", "Intermediary - Name", "Intermediary - Reference No etc", "Intermediary  - Address", "Intermediary - State", "Intermediary - Postcode, zip or similar", "Intermediary - Country (see code list)", "Policy issuance date", "Industrial sector of the insured ", "New/Renewal", "Logging 97111 Payroll", "Logging 97111 Premium", "LL End Limit", "Debit/Credit Mod" });

    private static readonly LondonHeaderRows CommercialAutoHeaders = new(
        new object?[] { "Ref", "CR0013", "CR0014", "CR0005", "CR0001", "CR0002", "CR0007", "CR0017", "CR0016", "CR0019", "CR0010", "CR0029", "CR0035", "CR0039", "CR0041", "CR0030", "CR0031", "CR0050", "CR0048", "CR0056", "CR0057", "CR0020", "CR0021", "CR0059", "CR0061", "CR0062", "CR0065", "CR0066", "CR0025", "CR0088", "CR0096", "CR0097", "CR0098", "CR0099", "CR0100", "CR0101", "CR0102", "CR0038", "CR0040", "CR0046", "CR0047", "CR0049", "CR0226", "CR0051", null, null, "CR0052", "CR0054", "CR0055", null, null, null, null, null, null, null, null, null, "CR0315", "CR1298", null, null, null },
        new object?[] { "Field", "Coverholder Name", "Coverholder PIN", "Unique Market Reference (UMR)", "Reporting Period Start Date", "Reporting Period (End Date)", "Section No", "Class of Business", "Risk Code", "Type of Insurance (Direct or Type or Reinsurance)", "Year of Account", "Certificate Ref", "Insured Full Name, Last Name or Company Name", "Insured Country Sub-division: State, Province, Territory, Canton etc.", "Insured Country (see code list)", "Risk Inception Date", "Risk Expiry Date", "Location of risk - Country", "Location of Risk - Country Sub-division: State, Province, Territory, Canton etc.", "Transaction Type - Original Premium etc.", "Effective Date of Transaction", "Original Currency", "Total gross written premium", "Gross premium paid this time", "Commission %", "Commission Amount", "Net Premium to London in original currency", "Settlement Currency", "US Classification", "State of Filing (see code list)", "Surplus Lines Broker Name", "Surplus Lines Broker Licence No ", "New Jersey SLA No", "Surplus Lines Broker Address", "Surplus Lines Broker State", "Surplus Lines Broker Zip Code", "Surplus Lines Broker Country", "Insured Address ", "Insured Postcode, Zip Code or Similar", "Location of Risk, Address", "Location of Risk, County", "Location of Risk, Postcode, zip code or similar", "Country of Registration", "Sum Insured Currency (see code list)", "Sum Insured Occurrence", "Sum Insured Aggregate", "Vehicle Information", "Deductible or Excess Amount", "Deductible or Excess Basis", "Other Fees or Deductions Description", "Other Fees or Deductions Amount", "Intermediary - Role", "Intermediary - Name", "Intermediary - Reference No etc", "Intermediary  - Address", "Intermediary - State", "Intermediary - Postcode, zip or similar", "Intermediary - Country (see code list)", "Policy issuance date", "Industrial sector of the insured ", "Add'l Rate Increase/Decrease Percent", "New/Renewal", "Debit/Credit Mod" });

    private static readonly LondonHeaderRows InlandMarineHeaders = new(
        new object?[] { "Ref", "CR0013", "CR0014", "CR0005", "CR0001", "CR0002", "CR0007", "CR0017", "CR0016", "CR0019", "CR0010", "CR0029", "CR0035", "CR0039", "CR0041", "CR0030", "CR0031", "CR0050", "CR0048", "CR0056", "CR0057", "CR0020", "CR0021", "CR0054", "CR0059", "CR0061", "CR0062", null, null, "CR0065", "CR0066", "CR0025", "CR0088", "CR0096", "CR0097", "CR0098", "CR0099", "CR0100", "CR0101", "CR0102", "CR0038", "CR0040", "CR0046", "CR0047", "CR0049", "CR0226", "CR0051", "CR0052", null, null, "CR0054", "CR0055", null, null, null, null, null, null, null, null, null, "CR0315", "CR1298", null, null, null, null },
        new object?[] { "Field", "Coverholder Name", "Coverholder PIN", "Unique Market Reference (UMR)", "Reporting Period Start Date", "Reporting Period (End Date)", "Section No", "Class of Business", "Risk Code", "Type of Insurance (Direct or Type or Reinsurance)", "Year of Account", "Certificate Ref", "Insured Full Name, Last Name or Company Name", "Insured Country Sub-division: State, Province, Territory, Canton etc.", "Insured Country (see code list)", "Risk Inception Date", "Risk Expiry Date", "Location of risk - Country", "Location of Risk - Country Sub-division: State, Province, Territory, Canton etc.", "Transaction Type - Original Premium etc.", "Effective Date of Transaction", "Original Currency", "Total gross written premium", "Deductible or Excess Amount (minimum)", "Gross premium paid this time", "Commission %", "Commission Amount", "Brokerage %", "Brokerage Amount", "Net Premium to London in original currency", "Settlement Currency", "US Classification", "State of Filing (see code list)", "Surplus Lines Broker Name", "Surplus Lines Broker Licence No ", "New Jersey SLA No", "Surplus Lines Broker Address", "Surplus Lines Broker State", "Surplus Lines Broker Zip Code", "Surplus Lines Broker Country", "Insured Address ", "Insured Postcode, Zip Code or Similar", "Location of Risk, Address", "Location of Risk, County", "Location of Risk, Postcode, zip code or similar", "Country of Registration", "Sum Insured Currency (see code list)", "Sum Insured Amount Occurrence Limit", "Sum Insured Amount Aggregate Limit", "Total Insurable Value", "Deductible or Excess Amount", "Deductible or Excess Basis", "Other Fees or Deductions Description", "Other Fees or Deductions Amount", "Intermediary - Role", "Intermediary - Name", "Intermediary - Reference No etc", "Intermediary  - Address", "Intermediary - State", "Intermediary - Postcode, zip or similar", "Intermediary - Country (see code list)", "Policy issuance date", "Industrial sector of the insured ", "Add'l Rate Increase/Decrease Percent", "New/Renewal", "Rate", "Debit/Credit Mod" });

    private sealed record LondonHeaderRows(IReadOnlyList<object?> Refs, IReadOnlyList<object?> Fields);
    private sealed record WorksheetData(string Name, IReadOnlyList<IReadOnlyList<object?>> Rows);
}

internal sealed record BordereauxLondonPremiumRow(
    BordereauxPremiumPreviewRowDto Source,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CoverholderName,
    string CoverholderPin,
    string Umr,
    string SectionNumber,
    string ClassOfBusiness,
    string RiskCode,
    string InsuranceType,
    string YearOfAccount,
    string CurrencyCode,
    decimal CommissionRate,
    decimal CommissionAmount,
    decimal NetPremiumToLondon,
    IReadOnlyList<BordereauxAutoVehicleDetail> AutoVehicles,
    IReadOnlyList<BordereauxInlandMarineUnitDetail> ImUnits);

internal sealed record BordereauxAutoVehicleDetail(
    string CertificateRef,
    int Number,
    int? YearMade,
    string Make,
    string Model,
    string Vin,
    string Type,
    decimal? ActualCashValue,
    decimal? Deductible,
    decimal? Premium,
    decimal? Rate);

internal sealed record BordereauxInlandMarineUnitDetail(
    string CertificateRef,
    int Number,
    int? YearMade,
    string Make,
    string Model,
    string Serial,
    string Type,
    decimal? ActualCashValue,
    string ActualCashValueNote,
    decimal? Deductible,
    decimal? Premium,
    decimal? Rate,
    string TransactionType);
