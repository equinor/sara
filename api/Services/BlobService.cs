using System.Globalization;
using api.Database.Models;
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

        public BlobStorageLocation CreateRawBlobStorageLocation(
            string rawStorageAccount,
            string rawBlobContainer,
            string rawBlobName
        );

        public BlobStorageLocation CreateAnonymizedBlobStorageLocation(
            string blobContainer,
            string blobName
        );

        public BlobStorageLocation CreateVisualizedBlobStorageLocation(
            string blobContainer,
            string blobName,
            string postfixAnalysisType
        );
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

    public class BlobService(
        ILogger<BlobService> logger,
        IOptions<BlobOptions> blobOptions,
        IConfiguration configuration
    ) : IBlobService
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

        private static string PostfixAnalysisTypeToBlobName(
            string blobName,
            string analysisTypePostfix
        )
        {
            var blobNameComponents = blobName.Split(".");
            if (blobNameComponents.Length != 2)
            {
                throw new InvalidOperationException(
                    $"Invalid blobName, containing multiple dots: {blobName}"
                );
            }

            return blobNameComponents[0] + "_" + analysisTypePostfix + "." + blobNameComponents[1];
        }

        public BlobStorageLocation CreateRawBlobStorageLocation(
            string rawStorageAccount,
            string rawBlobContainer,
            string rawBlobName
        )
        {
            var configRawStorageAccount = configuration.GetSection("Storage")["RawStorageAccount"];
            if (!rawStorageAccount.Equals(configRawStorageAccount))
            {
                throw new InvalidOperationException(
                    $"Incoming storage account, {rawStorageAccount}, is not equal to storage account in config, {configRawStorageAccount}."
                );
            }
            return new BlobStorageLocation
            {
                StorageAccount = rawStorageAccount,
                BlobContainer = rawBlobContainer,
                BlobName = rawBlobName,
            };
        }

        public BlobStorageLocation CreateAnonymizedBlobStorageLocation(
            string blobContainer,
            string blobName
        )
        {
            var anonymizedStorageAccount = configuration.GetSection("Storage")[
                "AnonStorageAccount"
            ];
            if (string.IsNullOrEmpty(anonymizedStorageAccount))
            {
                throw new InvalidOperationException("AnonStorageAccount is not configured.");
            }
            return new BlobStorageLocation
            {
                StorageAccount = anonymizedStorageAccount,
                BlobContainer = blobContainer,
                BlobName = blobName,
            };
        }

        public BlobStorageLocation CreateVisualizedBlobStorageLocation(
            string blobContainer,
            string blobName,
            string postfixAnalysisType
        )
        {
            var visualizedStorageAccount = configuration.GetSection("Storage")["VisStorageAccount"];
            if (string.IsNullOrEmpty(visualizedStorageAccount))
            {
                throw new InvalidOperationException("VisStorageAccount is not configured.");
            }

            return new BlobStorageLocation
            {
                StorageAccount = visualizedStorageAccount,
                BlobContainer = blobContainer,
                BlobName = PostfixAnalysisTypeToBlobName(blobName, postfixAnalysisType),
            };
        }
    }
}
