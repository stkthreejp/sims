namespace IMS.Application.Interfaces.Services;

public interface IBlobStorageService
{
    /// <summary>Uploads a stream and returns the blob path (name).</summary>
    Task<string> UploadAsync(Stream content, string fileName, string contentType);

    /// <summary>Returns a short-lived signed URL for direct browser download.</summary>
    Task<string> GetDownloadUrlAsync(string blobPath, string fileName, TimeSpan? expiry = null);

    /// <summary>Downloads a blob and returns its bytes.</summary>
    Task<byte[]> DownloadAsync(string blobPath);

    /// <summary>Permanently deletes the blob.</summary>
    Task DeleteAsync(string blobPath);
}
