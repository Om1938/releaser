using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Releaser.Domain.Common;
using Releaser.Domain.Targeting;
using Releaser.Electron;
using Releaser.Server.Infrastructure.Caching;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Feed;

/// <summary>
/// Renders the electron-updater metadata for an offered release. Cached per configuration version, so note changes apply
/// within the freshness bound. Returns null when the manifest no longer exists: a node still inside its freshness window
/// may decide to offer a release that was just obliterated (ADR 0014), and that must degrade to "no update".
/// </summary>
internal sealed class FeedManifestRenderer(HybridCache cache, IServiceScopeFactory scopeFactory)
{
    public async Task<string?> RenderAsync(FeedSnapshot snapshot, ReleaseId releaseId, PlatformTarget platform, CancellationToken cancellationToken) =>
        await cache.GetOrCreateAsync(
            $"feed:{snapshot.AppId}:{snapshot.Resolution.ConfigVersion}:{releaseId}:{platform}",
            (ReleaseId: releaseId, Platform: platform),
            async (state, token) => await LoadAsync(state.ReleaseId, state.Platform, token),
            new HybridCacheEntryOptions { Expiration = TimeSpan.FromHours(1), LocalCacheExpiration = TimeSpan.FromMinutes(10) },
            tags: [$"app:{snapshot.AppId}"],
            cancellationToken: cancellationToken);

    private async Task<string?> LoadAsync(ReleaseId releaseId, PlatformTarget platform, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReleaserDbContext>();
        var content = await db.ReleaseManifests.AsNoTracking()
            .Where(m => m.ReleaseId == releaseId && m.Platform == platform)
            .Select(m => m.Content)
            .SingleOrDefaultAsync(cancellationToken);
        if (content is null)
        {
            return null;
        }
        var note = await db.ReleaseNotes.AsNoTracking()
            .SingleOrDefaultAsync(n => n.ReleaseId == releaseId && n.State == Domain.ReleaseNotes.ReleaseNoteState.Published, cancellationToken);
        return ElectronManifest.Parse(content).RenderForFeed(note?.ToMarkdown());
    }
}
