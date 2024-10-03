using System.Globalization;
using System.Text;
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
        public Task<byte[]> DownloadBlob(string blobName, string containerName, string accountName);

        public AsyncPageable<BlobItem> FetchAllBlobs(string containerName, string accountName);

    }

    public class BlobService(ILogger<BlobService> logger, IOptions<AzureAdOptions> azureOptions) : IBlobService
    {
        public async Task<byte[]> DownloadBlob(string blobName, string containerName, string accountName)
        {
            var blobContainerClient = GetBlobContainerClient(containerName, accountName);
            var blobClient = blobContainerClient.GetBlobClient(blobName);

            using var memoryStream = new MemoryStream();
            await blobClient.DownloadToAsync(memoryStream);

            return memoryStream.ToArray();
        }

        public AsyncPageable<BlobItem> FetchAllBlobs(string containerName, string accountName)
        {
            var blobContainerClient = GetBlobContainerClient(containerName, accountName);
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

        private BlobContainerClient GetBlobContainerClient(string containerName, string accountName)
        {
            var serviceClient = new BlobServiceClient(
                new Uri($"https://{accountName}.blob.core.windows.net"),
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
