using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using api.Database.Models;
using api.Services;

namespace Api.Test.Mocks;

public class BlobStorageServiceMock : IBlobStorageService
{
    public bool BlobExists { get; set; } = true;
    private readonly ConcurrentQueue<(
        BlobStorageLocation Location,
        byte[] Content,
        string ContentType
    )> _uploads = new();
    public IReadOnlyCollection<(
        BlobStorageLocation Location,
        byte[] Content,
        string ContentType
    )> Uploads => _uploads.ToArray();

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
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        _uploads.Enqueue((destination, buffer.ToArray(), contentType));
    }

    public async Task CopyBlobAsync(BlobStorageLocation source, BlobStorageLocation destination)
    {
        using var sourceStream = await DownloadBlobAsync(source);
        await UploadBlobAsync(destination, sourceStream, "application/octet-stream");
    }

    public async Task<Uri?> TryCreateReadSasUriAsync(BlobStorageLocation location)
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
