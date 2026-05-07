using Microsoft.AspNetCore.Http;

namespace SIMS.Application.Interfaces.Services;

public interface IFileScanService
{
    Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public sealed record FileScanResult(bool IsAllowed, string? ErrorCode = null, string? ErrorMessage = null)
{
    public static FileScanResult Clean() => new(true);

    public static FileScanResult Blocked(string errorCode, string errorMessage) =>
        new(false, errorCode, errorMessage);
}
