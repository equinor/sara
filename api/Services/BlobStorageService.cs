using api.Database.Models;
using Azure.Core;
using Azure.Storage.Blobs;

namespace api.Services;

public interface IBlobStorageService
{
    Task<MemoryStream> DownloadBlobAsync(BlobStorageLocation location);
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
