using System.Globalization;
using api.Configurations;
using Azure;
using Azure.Identity;
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

    public class BlobOptions
    {
        public string RawStorageAccount { get; set; } = "";
        public string AnonStorageAccount { get; set; } = "";
        public string VisStorageAccount { get; set; } = "";
    }

    public class BlobService(ILogger<BlobService> logger, IOptions<AzureAdOptions> azureOptions, IOptions<BlobOptions> blobOptions) : IBlobService
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
            var serviceClient = new BlobServiceClient(
                new Uri($"https://{blobOptions.Value.AnonStorageAccount}.blob.core.windows.net"),
                new ClientSecretCredential(
                    azureOptions.Value.TenantId,
                    azureOptions.Value.ClientId,
                    azureOptions.Value.ClientSecret
                )
            );
            var containerClient = serviceClient.GetBlobContainerClient(containerName.ToLower(CultureInfo.CurrentCulture));
            return containerClient;
        }
    }
}
