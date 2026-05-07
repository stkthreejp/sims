using System.Buffers.Binary;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SIMS.Application.Interfaces.Services;

namespace SIMS.Infrastructure.Services;

public sealed class ClamAvFileScanService : IFileScanService
{
    private const int DefaultPort = 3310;
    private const int DefaultChunkSize = 8192;

    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _timeout;
    private readonly ILogger<ClamAvFileScanService> _logger;

    public ClamAvFileScanService(IConfiguration config, ILogger<ClamAvFileScanService> logger)
    {
        _host = config["Uploads:MalwareScanning:ClamAv:Host"] ?? "localhost";
        _port = int.TryParse(config["Uploads:MalwareScanning:ClamAv:Port"], out var port) ? port : DefaultPort;
        _timeout = TimeSpan.FromSeconds(
            int.TryParse(config["Uploads:MalwareScanning:TimeoutSeconds"], out var seconds) ? seconds : 30);
        _logger = logger;
    }

    public async Task<FileScanResult> ScanAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, timeoutCts.Token);

            await using var network = client.GetStream();
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeoutCts.Token);

            await using var fileStream = file.OpenReadStream();
            var buffer = new byte[DefaultChunkSize];
            var lengthPrefix = new byte[4];

            int read;
            while ((read = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeoutCts.Token)) > 0)
            {
                BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, read);
                await network.WriteAsync(lengthPrefix, timeoutCts.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), timeoutCts.Token);
            }

            BinaryPrimitives.WriteInt32BigEndian(lengthPrefix, 0);
            await network.WriteAsync(lengthPrefix, timeoutCts.Token);

            var responseBuffer = new byte[1024];
            var responseLength = await network.ReadAsync(responseBuffer, timeoutCts.Token);
            var response = System.Text.Encoding.UTF8.GetString(responseBuffer, 0, responseLength);

            if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Upload malware scan blocked file {FileName}: ClamAV reported a detection.", file.FileName);
                return FileScanResult.Blocked("MALWARE_DETECTED", "The uploaded file failed malware scanning.");
            }

            if (response.Contains("OK", StringComparison.OrdinalIgnoreCase))
                return FileScanResult.Clean();

            _logger.LogWarning("Upload malware scan returned an unexpected ClamAV response for {FileName}: {Response}", file.FileName, response);
            return FileScanResult.Blocked("FILE_SCAN_FAILED", "The uploaded file could not be scanned.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Upload malware scan timed out for {FileName}", file.FileName);
            return FileScanResult.Blocked("FILE_SCAN_FAILED", "The uploaded file could not be scanned.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upload malware scan failed for {FileName}", file.FileName);
            return FileScanResult.Blocked("FILE_SCAN_FAILED", "The uploaded file could not be scanned.");
        }
    }
}
