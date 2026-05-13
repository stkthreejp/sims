namespace SIMS.Application.Interfaces.Services;

public interface IHtmlToPdfService
{
    Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default);
}
