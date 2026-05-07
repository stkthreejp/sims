using Microsoft.AspNetCore.Http;
using SIMS.Application.Interfaces.Services;
using SIMS.Application.Services;
using Xunit;

namespace SIMS.Application.Tests.Security;

public class FileScanServiceTests
{
    [Fact]
    public async Task NoOpScanner_AllowsFile()
    {
        var service = new NoOpFileScanService();
        var file = new FormFile(new MemoryStream([1, 2, 3]), 0, 3, "file", "test.pdf");

        var result = await service.ScanAsync(file);

        Assert.True(result.IsAllowed);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void BlockedResult_CarriesStableErrorCode()
    {
        var result = FileScanResult.Blocked("MALWARE_DETECTED", "The uploaded file failed malware scanning.");

        Assert.False(result.IsAllowed);
        Assert.Equal("MALWARE_DETECTED", result.ErrorCode);
    }
}
