using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Releaser.Domain.Common;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Infrastructure.Caching;

/// <summary>
/// Bounded-freshness access to feed snapshots (ADR 0008):
/// a per-node version stamp lives at most <see cref="ResolutionOptions.FreshnessSeconds"/>;
/// snapshots are cached (L1 + optional Redis L2) under keys that include the configuration version, so they are never stale.
/// When PostgreSQL is unreachable after the stamp expires, callers get an exception and must fail closed.
/// </summary>
public sealed class FeedSnapshotCache(
    IMemoryCache stamps,
    HybridCache cache,
    IServiceScopeFactory scopeFactory,
    IOptions<ResolutionOptions> options) : IConfigVersionListener
{
    private readonly ConcurrentDictionary<AppId, string> _stampKeysById = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<VersionStamp?>>> _stampLoads = new();

    public async Task<FeedSnapshot?> GetAsync(ApplicationKey key, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var stamp = await GetStampAsync(key, cancellationToken);
            if (stamp is null)
            {
                return null;
            }
            try
            {
                return await GetSnapshotAsync(stamp, cancellationToken);
            }
            catch (StaleStampException) when (attempt == 0)
            {
                stamps.Remove(StampKey(key));
            }
        }
    }

    public void ConfigVersionChanged(AppId application)
    {
        if (_stampKeysById.TryGetValue(application, out var stampKey))
        {
            stamps.Remove(stampKey);
        }
    }

    private async Task<VersionStamp?> GetStampAsync(ApplicationKey key, CancellationToken cancellationToken)
    {
        var stampKey = StampKey(key);
        if (stamps.TryGetValue(stampKey, out VersionStamp? cached))
        {
            return cached;
        }
        // Single flight: concurrent misses for one application share one database round trip.
        var load = _stampLoads.GetOrAdd(stampKey, _ => new Lazy<Task<VersionStamp?>>(() => LoadStampAsync(key, stampKey)));
        try
        {
            return await load.Value.WaitAsync(cancellationToken);
        }
        finally
        {
            _stampLoads.TryRemove(new KeyValuePair<string, Lazy<Task<VersionStamp?>>>(stampKey, load));
        }
    }

    private async Task<VersionStamp?> LoadStampAsync(ApplicationKey key, string stampKey)
    {
        var cancellationToken = CancellationToken.None; // shared by several callers; each caller applies its own cancellation
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReleaserDbContext>();
        var stamp = await db.Applications.AsNoTracking()
            .Where(a => a.Key == key)
            .Select(a => new VersionStamp(a.Id, a.ConfigVersion))
            .SingleOrDefaultAsync(cancellationToken);
        stamps.Set(stampKey, stamp, TimeSpan.FromSeconds(options.Value.FreshnessSeconds));
        if (stamp is not null)
        {
            _stampKeysById[stamp.AppId] = stampKey;
        }
        return stamp;
    }

    private async Task<FeedSnapshot> GetSnapshotAsync(VersionStamp stamp, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"snapshot:{stamp.AppId}:{stamp.ConfigVersion}",
            stamp,
            async (state, token) => await LoadExactAsync(state, token),
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(1),
                LocalCacheExpiration = TimeSpan.FromMinutes(options.Value.SnapshotLocalMinutes),
            },
            tags: [$"app:{stamp.AppId}"],
            cancellationToken: cancellationToken);

    private async Task<FeedSnapshot> LoadExactAsync(VersionStamp stamp, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var loader = scope.ServiceProvider.GetRequiredService<FeedSnapshotLoader>();
        var snapshot = await loader.LoadAsync(stamp.AppId, cancellationToken);
        // Never store data under a version key it does not belong to.
        return snapshot.Resolution.ConfigVersion == stamp.ConfigVersion ? snapshot : throw new StaleStampException();
    }

    private static string StampKey(ApplicationKey key) => $"stamp:{key.Value}";

    private sealed record VersionStamp(AppId AppId, long ConfigVersion);

    private sealed class StaleStampException : Exception;
}
