using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace api.Services;

public interface IUserDelegationKeyProvider
{
    Task<UserDelegationKey> GetAsync(
        BlobServiceClient serviceClient,
        string storageAccount,
        TimeSpan sasLifetime,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
///     Caches user delegation keys per storage account.
///
///     A delegation key is account-wide and valid for the window it was
///     requested for, so fetching one per blob is wasted work: every SAS in a
///     response can be signed with the same key. The uncached path made one
///     network call to Azure Storage per blob, which is the dominant cost of
///     the inspection-record endpoints.
/// </summary>
public class UserDelegationKeyProvider : IUserDelegationKeyProvider
{
    /// How long each fetched key is valid for. Must exceed the SAS lifetime,
    /// because a SAS may not outlive the key that signed it.
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(2);

    /// Absorbs clock skew between this process and Azure Storage, both when
    /// backdating the key start and when deciding a cached key is still usable.
    private static readonly TimeSpan ClockSkewBuffer = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly TimeProvider _timeProvider;

    public UserDelegationKeyProvider(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<UserDelegationKey> GetAsync(
        BlobServiceClient serviceClient,
        string storageAccount,
        TimeSpan sasLifetime,
        CancellationToken cancellationToken = default
    )
    {
        var now = _timeProvider.GetUtcNow();

        if (_cache.TryGetValue(storageAccount, out var cached) && cached.IsUsableAt(now))
            return cached.Key;

        // Serialise refreshes per account so an expiry does not send every
        // in-flight request to Azure Storage at once.
        var gate = _locks.GetOrAdd(storageAccount, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            now = _timeProvider.GetUtcNow();
            if (_cache.TryGetValue(storageAccount, out cached) && cached.IsUsableAt(now))
                return cached.Key;

            var startsOn = now - ClockSkewBuffer;
            var expiresOn = now + KeyLifetime;

            var key = await serviceClient.GetUserDelegationKeyAsync(
                startsOn,
                expiresOn,
                cancellationToken
            );

            // A SAS signed at time t expires at t + sasLifetime, which must stay
            // inside the key's window. Stop reusing the key early enough that
            // this always holds.
            var reusableUntil = expiresOn - sasLifetime - ClockSkewBuffer;
            _cache[storageAccount] = new CacheEntry(key.Value, reusableUntil);
            return key.Value;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record CacheEntry(UserDelegationKey Key, DateTimeOffset ReusableUntil)
    {
        public bool IsUsableAt(DateTimeOffset now) => now < ReusableUntil;
    }
}
