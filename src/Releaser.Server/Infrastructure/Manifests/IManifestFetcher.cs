namespace Releaser.Server.Infrastructure.Manifests;

/// <summary>Downloads an external update manifest (metadata only — never binaries).</summary>
public interface IManifestFetcher
{
    Task<FetchedManifest> FetchAsync(Uri url, CancellationToken cancellationToken);
}

public sealed record FetchedManifest(Uri Url, string Content, string Sha256);

/// <summary>The manifest could not be retrieved; maps to HTTP 422. <see cref="StatusCode"/> is set when the host answered.</summary>
public sealed class ManifestFetchException(string message, System.Net.HttpStatusCode? statusCode = null, Exception? inner = null) : Exception(message, inner)
{
    public System.Net.HttpStatusCode? StatusCode { get; } = statusCode;

    /// <summary>The host answered in a way that usually means the file is not (yet) there.</summary>
    public bool IsMissingFile => StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Forbidden;
}
