using SIMS.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace SIMS.Infrastructure.Services;

/// <summary>
/// Renders PDF pages to PNG using PDFtoImage (SkiaSharp + bundled PDFium — no Ghostscript).
/// SkiaSharp Linux native assets are already referenced for the Azure App Service (Linux)
/// host. Failures degrade gracefully: the intake worker treats an empty result as
/// "no renderable pages" and marks the job for review rather than crashing.
/// </summary>
public class PdfToImagePageRenderer : IPdfPageRenderer
{
    private readonly ILogger<PdfToImagePageRenderer> _logger;

    public PdfToImagePageRenderer(ILogger<PdfToImagePageRenderer> logger) => _logger = logger;

    public IReadOnlyList<byte[]> RenderPdfToPngPages(byte[] pdfBytes, CancellationToken ct = default)
    {
        var pages = new List<byte[]>();
        try
        {
            foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfBytes))
            {
                ct.ThrowIfCancellationRequested();
                using (bitmap)
                using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 85))
                {
                    pages.Add(data.ToArray());
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PDF render failed after {Count} page(s).", pages.Count);
        }
        return pages;
    }
}
