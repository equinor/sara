using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace api.Services
{
    public interface IBlobService
    {
        public Task<byte[]> DownloadBlob(string blobName, string containerName);

        public AsyncPageable<BlobItem> FetchAllBlobs(string containerName);
    }

    public record BlobOptions
    {
        public string RawStorageAccount { get; init; } = "";
        public string RawConnectionString { get; init; } = "";
        public string AnonStorageAccount { get; init; } = "";
        public string AnonConnectionString { get; init; } = "";
        public string VisStorageAccount { get; init; } = "";
        public string VisConnectionString { get; init; } = "";
    }

    public class BlobService(ILogger<BlobService> logger, IOptions<BlobOptions> blobOptions)
        : IBlobService
    {
        public async Task<byte[]> DownloadBlob(string blobName, string containerName)
        {
            var blobContainerClient = GetBlobContainerClient(containerName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            using var memoryStream = new MemoryStream();
            await blobClient.DownloadToAsync(memoryStream);

            return memoryStream.ToArray();
        }

        public AsyncPageable<BlobItem> FetchAllBlobs(string containerName)
        {
            var blobContainerClient = GetBlobContainerClient(containerName);
            try
            {
                return blobContainerClient.GetBlobsAsync(BlobTraits.Metadata);
            }
            catch (RequestFailedException e)
            {
                string errorMessage = $"Failed to fetch blob items because: {e.Message}";
                logger.LogError(e, "{ErrorMessage}", errorMessage);
                throw;
            }
        }

        // TODO: Set up possibility to use different containers
        private BlobContainerClient GetBlobContainerClient(string containerName)
        {
            var serviceClient = new BlobServiceClient(blobOptions.Value.AnonConnectionString);
            var containerClient = serviceClient.GetBlobContainerClient(
                containerName.ToLower(CultureInfo.CurrentCulture)
            );
            return containerClient;
        }
    }
}
