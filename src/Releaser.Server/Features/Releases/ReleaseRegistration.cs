using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Releases;
using Releaser.Electron;
using Releaser.Server.Infrastructure.Manifests;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Releases;

/// <summary>Fetches, validates and snapshots external manifests, then registers the release (ADR 0002, ADR 0003).</summary>
internal sealed class ReleaseRegistration(ReleaserDbContext db, IManifestFetcher fetcher, TimeProvider clock)
{
    public async Task<(Release Release, List<ReleaseManifest> Manifests)> RegisterAsync(AppId appId, RegisterReleaseRequest request, CancellationToken cancellationToken)
    {
        var version = SemanticVersion.Parse(request.Version);
        var channels = request.Channels.Distinct().Select(ChannelKey.From).ToList();
        await EnsureChannelsExistAsync(appId, channels, cancellationToken);
        await EnsureVersionIsNewAsync(appId, version, cancellationToken);

        var fetched = await Task.WhenAll(request.Manifests.Select(reference => SnapshotAsync(reference, version, cancellationToken)));
        var now = clock.GetUtcNow();
        var release = Release.Register(appId, version, request.Title, fetched.Select(f => f.Reference.Platform), now);
        channels.ForEach(release.AssignChannel);
        var manifests = fetched
            .Select(f => ReleaseManifest.Snapshot(appId, release.Id, f.Reference.Platform, f.Source.Url, f.Source.Sha256, f.Manifest.ToYaml(), now))
            .ToList();
        db.Releases.Add(release);
        db.ReleaseManifests.AddRange(manifests);
        return (release, manifests);
    }

    public async Task<(ElectronManifest Manifest, FetchedManifest Source)> PreviewAsync(Uri url, CancellationToken cancellationToken)
    {
        var source = await fetcher.FetchAsync(url, cancellationToken);
        return (ElectronManifest.Parse(source.Content).WithAbsoluteUrls(source.Url), source);
    }

    /// <summary>Adds one more platform's manifest to an existing release (append-only; ADR 0005).</summary>
    public async Task<ReleaseManifest> AddManifestAsync(Release release, ManifestReference reference, CancellationToken cancellationToken)
    {
        release.AddPlatform(reference.Platform);
        var (_, source, manifest) = await SnapshotAsync(reference, release.Version, cancellationToken);
        var snapshot = ReleaseManifest.Snapshot(release.AppId, release.Id, reference.Platform, source.Url, source.Sha256, manifest.ToYaml(), clock.GetUtcNow());
        db.ReleaseManifests.Add(snapshot);
        return snapshot;
    }

    private async Task<(ManifestReference Reference, FetchedManifest Source, ElectronManifest Manifest)> SnapshotAsync(
        ManifestReference reference, SemanticVersion version, CancellationToken cancellationToken)
    {
        ElectronManifest manifest;
        FetchedManifest source;
        try
        {
            (manifest, source) = await PreviewAsync(new Uri(reference.Url), cancellationToken);
        }
        catch (ManifestFetchException exception)
        {
            throw new ManifestFetchException($"{reference.Platform} manifest: {exception.Message}", exception);
        }
        if (manifest.Version != version)
        {
            throw new DomainRuleException("manifest.version_mismatch",
                $"The {reference.Platform} manifest declares version {manifest.Version}, not {version}.");
        }
        return (reference, source, manifest);
    }

    private async Task EnsureChannelsExistAsync(AppId appId, List<ChannelKey> channels, CancellationToken cancellationToken)
    {
        var existing = await db.Channels.Where(c => c.AppId == appId).Select(c => c.Key).ToListAsync(cancellationToken);
        var missing = channels.Except(existing).ToList();
        if (missing.Count > 0)
        {
            throw new DomainRuleException("release.unknown_channel", $"Unknown channel(s): {string.Join(", ", missing)}.");
        }
    }

    private async Task EnsureVersionIsNewAsync(AppId appId, SemanticVersion version, CancellationToken cancellationToken)
    {
        if (await db.Releases.AnyAsync(r => r.AppId == appId && r.Version == version, cancellationToken))
        {
            throw new DomainRuleException("release.duplicate_version", $"Version {version} is already registered; release identities are immutable.");
        }
    }
}
