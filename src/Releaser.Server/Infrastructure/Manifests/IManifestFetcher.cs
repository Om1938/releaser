namespace Releaser.Server.Infrastructure.Manifests;

/// <summary>Downloads an external update manifest (metadata only — never binaries).</summary>
public interface IManifestFetcher
{
    Task<FetchedManifest> FetchAsync(Uri url, CancellationToken cancellationToken);
}

public sealed record FetchedManifest(Uri Url, string Content, string Sha256);

/// <summary>The manifest could not be retrieved; maps to HTTP 422 at registration.</summary>
public sealed class ManifestFetchException(string message, Exception? inner = null) : Exception(message, inner);
