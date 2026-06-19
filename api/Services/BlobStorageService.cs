using api.Database.Models;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace api.Services;

public interface IBlobStorageService
{
    Task<MemoryStream> DownloadBlobAsync(BlobStorageLocation location);

    Task UploadBlobAsync(BlobStorageLocation destination, Stream content, string contentType);

    Task CopyBlobAsync(BlobStorageLocation source, BlobStorageLocation destination);
}

public class BlobStorageService(TokenCredential credential, IConfiguration configuration)
    : IBlobStorageService
{
    public async Task<MemoryStream> DownloadBlobAsync(BlobStorageLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.StorageAccount))
            throw new InvalidOperationException("BlobStorageLocation.StorageAccount is empty.");

        if (string.IsNullOrWhiteSpace(location.BlobContainer))
            throw new InvalidOperationException(
                $"BlobStorageLocation.BlobContainer is empty for storage account '{location.StorageAccount}'."
            );

        if (string.IsNullOrWhiteSpace(location.BlobName))
            throw new InvalidOperationException(
                $"BlobStorageLocation.BlobName is empty for storage account '{location.StorageAccount}/{location.BlobContainer}'."
            );

        var serviceClient = CreateBlobServiceClient(location.StorageAccount);

        var containerClient = serviceClient.GetBlobContainerClient(location.BlobContainer);
        var blobClient = containerClient.GetBlobClient(location.BlobName);

        var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        stream.Position = 0;
        return stream;
    }

    public async Task UploadBlobAsync(
        BlobStorageLocation destination,
        Stream content,
        string contentType
    )
    {
        if (string.IsNullOrWhiteSpace(destination.StorageAccount))
            throw new InvalidOperationException("BlobStorageLocation.StorageAccount is empty.");

        if (string.IsNullOrWhiteSpace(destination.BlobContainer))
            throw new InvalidOperationException(
                $"BlobStorageLocation.BlobContainer is empty for storage account '{destination.StorageAccount}'."
            );

        if (string.IsNullOrWhiteSpace(destination.BlobName))
            throw new InvalidOperationException(
                $"BlobStorageLocation.BlobName is empty for storage account '{destination.StorageAccount}/{destination.BlobContainer}'."
            );

        var serviceClient = CreateBlobServiceClient(destination.StorageAccount);
        var containerClient = serviceClient.GetBlobContainerClient(destination.BlobContainer);
        var blobClient = containerClient.GetBlobClient(destination.BlobName);

        content.Position = 0;
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            }
        );
    }

    public async Task CopyBlobAsync(BlobStorageLocation source, BlobStorageLocation destination)
    {
        using var sourceStream = await DownloadBlobAsync(source);
        await UploadBlobAsync(destination, sourceStream, "application/octet-stream");
    }

    private BlobServiceClient CreateBlobServiceClient(string accountName)
    {
        // Per-account connection string override (e.g. for Azurite in local dev).
        // Config key: BlobStorage:{accountName}:ConnectionString
        var connectionString = configuration[$"BlobStorage:{accountName}:ConnectionString"];
        if (!string.IsNullOrEmpty(connectionString))
        {
            return new BlobServiceClient(connectionString);
        }

        return new BlobServiceClient(
            new Uri($"https://{accountName}.blob.core.windows.net"),
            credential
        );
    }
}
