using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIMS.Application.DTOs.Accounting;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public class WireSheetPdfService : IWireSheetPdfService
{
    public byte[] Generate(BatchDetailDto batch, string companyName)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily(Fonts.Arial));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(companyName).Bold().FontSize(12);
                            c.Item().Text("WIRE INSTRUCTION SHEET").Bold().FontSize(10).FontColor("#1e3a5f");
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().Text($"Batch: {batch.BatchNumber}").Bold();
                            c.Item().Text($"Date: {DateTime.UtcNow:MMMM d, yyyy}");
                            c.Item().Text($"Total Wires: {batch.TotalWires}");
                            c.Item().Text($"Total Amount: {batch.TotalAmount:C}").Bold();
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#1e3a5f");
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    var fmt = (decimal d) => d.ToString("C");
                    var wireNum = 0;

                    foreach (var wire in batch.Wires)
                    {
                        wireNum++;
                        col.Item().PaddingBottom(10).Border(1).BorderColor("#cccccc").Column(wc =>
                        {
                            // Wire header
                            wc.Item()
                                .Background("#1e3a5f")
                                .Padding(6)
                                .Row(r =>
                                {
                                    r.RelativeItem().Text($"Wire #{wireNum} — {wire.PayeeName}").Bold().FontColor(Colors.White);
                                    r.ConstantItem(120).AlignRight().Text(fmt(wire.NetAmount)).Bold().FontColor(Colors.White);
                                });

                            // Source instructions table
                            wc.Item().Padding(6).Column(tc =>
                            {
                                tc.Item().Row(r =>
                                {
                                    r.ConstantItem(80).Text("Receipt #").Bold().FontColor("#555555");
                                    r.RelativeItem().Text("Fee Description").Bold().FontColor("#555555");
                                    r.ConstantItem(90).AlignRight().Text("Amount").Bold().FontColor("#555555");
                                });
                                tc.Item().PaddingBottom(3).LineHorizontal(0.5f).LineColor("#dddddd");

                                foreach (var inst in wire.Instructions)
                                {
                                    tc.Item().Row(r =>
                                    {
                                        r.ConstantItem(80).Text(inst.ReceiptNumber).FontSize(8);
                                        r.RelativeItem().Text(inst.FeeDisplayName).FontSize(8);
                                        r.ConstantItem(90).AlignRight().Text(fmt(inst.Amount)).FontSize(8);
                                    });
                                }
                            });
                        });
                    }

                    // Summary totals
                    col.Item().PaddingTop(6).AlignRight().Text($"TOTAL: {fmt(batch.TotalAmount)}")
                        .Bold().FontSize(11);

                    // Authorization block
                    col.Item().PaddingTop(30).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#000000");
                            c.Item().PaddingTop(3).Text("Authorized Signature / Date");
                        });
                        r.ConstantItem(30);
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().LineHorizontal(0.5f).LineColor("#000000");
                            c.Item().PaddingTop(3).Text("Reviewed By / Date");
                        });
                    });
                });

                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC — {batch.BatchNumber} — Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
            });
        }).GeneratePdf();
    }
}
