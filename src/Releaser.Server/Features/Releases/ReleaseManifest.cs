using Releaser.Domain.Common;
using Releaser.Domain.Targeting;

namespace Releaser.Server.Features.Releases;

/// <summary>Immutable snapshot of one externally hosted electron-builder manifest, with relative URLs made absolute (ADR 0002).</summary>
public sealed class ReleaseManifest : IApplicationScoped
{
    private ReleaseManifest() { } // EF Core

    public Guid Id { get; private set; }
    public AppId AppId { get; private set; }
    public ReleaseId ReleaseId { get; private set; }
    public PlatformTarget Platform { get; private set; }
    public string SourceUrl { get; private set; } = string.Empty;
    public string SourceSha256 { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset FetchedAt { get; private set; }

    public static ReleaseManifest Snapshot(AppId appId, ReleaseId releaseId, PlatformTarget platform, Uri sourceUrl, string sourceSha256, string content, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        AppId = appId,
        ReleaseId = releaseId,
        Platform = platform,
        SourceUrl = sourceUrl.AbsoluteUri,
        SourceSha256 = sourceSha256,
        Content = content,
        FetchedAt = now,
    };
}
