using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using IMS.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace IMS.Infrastructure.Services;

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["Storage:AzureBlobConnectionString"]
            ?? throw new InvalidOperationException("Azure Blob connection string is not configured.");
        var containerName = config["Storage:AzureBlobContainerName"] ?? "documents";
        _container = new BlobContainerClient(connectionString, containerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(Stream content, string fileName, string contentType)
    {
        // Use a GUID-based path to avoid collisions; keep original name readable in path
        var ext = Path.GetExtension(fileName);
        var blobPath = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}{ext}";

        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blobPath;
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null)
    {
        var blobClient = _container.GetBlobClient(blobPath);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry ?? TimeSpan.FromHours(1)),
            ContentDisposition = $"attachment; filename=\"{fileName}\"",
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    public async Task<byte[]> DownloadAsync(string blobPath)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        var response = await blobClient.DownloadContentAsync();
        return response.Value.Content.ToArray();
    }

    public async Task DeleteAsync(string blobPath)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync();
    }
}
