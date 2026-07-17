using System;
using System.IO;
using System.Threading.Tasks;
using api.Database.Models;
using api.Services;

namespace Api.Test.Mocks;

public class BlobStorageServiceMock : IBlobStorageService
{
    public bool BlobExists { get; set; } = true;

    public async Task<MemoryStream> DownloadBlobAsync(BlobStorageLocation location)
    {
        var stream = new MemoryStream();
        return stream;
    }

    public async Task UploadBlobAsync(
        BlobStorageLocation destination,
        Stream content,
        string contentType
    )
    {
        await Task.CompletedTask;
    }

    public async Task CopyBlobAsync(BlobStorageLocation source, BlobStorageLocation destination)
    {
        using var sourceStream = await DownloadBlobAsync(source);
        await UploadBlobAsync(destination, sourceStream, "application/octet-stream");
    }

    public async Task<Uri> CreateReadSasUri(BlobStorageLocation location)
    {
        var mockSAS = "blablablablablabla";
        return new Uri(
            $"https://{location.StorageAccount}.blob.core.windows.net/{location.BlobContainer}/{location.BlobName}?{mockSAS}"
        );
    }

    public async Task<bool> ExistsAsync(BlobStorageLocation location)
    {
        await Task.CompletedTask;
        return BlobExists;
    }
}
