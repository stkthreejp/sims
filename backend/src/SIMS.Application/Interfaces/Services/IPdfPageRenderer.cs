namespace SIMS.Application.Interfaces.Services;

/// <summary>Renders each page of a PDF to PNG bytes for vision analysis.</summary>
public interface IPdfPageRenderer
{
    /// <summary>Returns one PNG per page, in order. Empty if the input isn't a renderable PDF.</summary>
    IReadOnlyList<byte[]> RenderPdfToPngPages(byte[] pdfBytes, CancellationToken ct = default);
}
