using Microsoft.AspNetCore.Http;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Application.Services;

public sealed class NoOpFileScanService : IFileScanService
{
    public Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default) =>
        Task.FromResult(FileScanResult.Clean());
}
