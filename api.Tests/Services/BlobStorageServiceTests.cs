using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using api.Database.Models;
using api.Services;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Test.Services;

public class BlobStorageServiceTests
{
    private static BlobStorageLocation Location(string storageAccount) =>
        new()
        {
            StorageAccount = storageAccount,
            BlobContainer = "container",
            BlobName = "blob.jpg",
        };

    private static BlobStorageService CreateService(IUserDelegationKeyProvider keyProvider) =>
        new(
            Mock.Of<TokenCredential>(),
            new ConfigurationBuilder().Build(),
            keyProvider,
            NullLogger<BlobStorageService>.Instance
        );

    private static IUserDelegationKeyProvider KeyProviderThatThrows(Exception exception)
    {
        var mock = new Mock<IUserDelegationKeyProvider>();
        mock.Setup(p =>
                p.GetAsync(
                    It.IsAny<BlobServiceClient>(),
                    It.IsAny<string>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(exception);
        return mock.Object;
    }

    public static TheoryData<Exception> UnreachableAccountFailures =>
        new()
        {
            // Denied on an account that exists.
            new RequestFailedException(403, "AuthorizationPermissionMismatch"),
            // Renamed or deleted, so DNS resolution fails rather than authorization.
            new HttpRequestException("No such host is known."),
            // Already known to be unreachable, so no call was attempted.
            new UserDelegationKeyUnavailableException("an-account"),
            // The SDK's retry policy wraps any of the above.
            new AggregateException(new RequestFailedException(403, "retry policy wrapped")),
        };

    [Theory]
    [MemberData(nameof(UnreachableAccountFailures))]
    public async Task ReturnsNullWhenTheStorageAccountCannotBeReached(Exception failure)
    {
        var service = CreateService(KeyProviderThatThrows(failure));

        var sas = await service.TryCreateReadSasUriAsync(Location("an-account"));

        Assert.Null(sas);
    }
}
