using SIMS.Application.Interfaces.Services;
using Syncfusion.HtmlConverter;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;

namespace SIMS.Infrastructure.Services;

public class SyncfusionHtmlToPdfService : IHtmlToPdfService
{
    public Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default)
    {
        var converter = new HtmlToPdfConverter(HtmlRenderingEngine.Blink);
        converter.ConverterSettings = new BlinkConverterSettings
        {
            PdfPageSize = PdfPageSize.Letter,
            Margin = new PdfMargins { All = 0 },
            EnableJavaScript = true,
            AdditionalDelay = 1500,
        };

        using var document = converter.Convert(html, string.Empty);
        using var stream = new MemoryStream();
        document.Save(stream);

        return Task.FromResult(stream.ToArray());
    }
}
