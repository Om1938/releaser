using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Releaser.Domain.Targeting;
using Releaser.Electron;
using Releaser.Server.Infrastructure.Manifests;

namespace Releaser.Server.Tests.Infrastructure;

/// <summary>Stands in for an external CDN that hosts electron-builder manifests (with relative file paths, as electron-builder writes them).</summary>
public sealed class StubManifestHost : IManifestFetcher
{
    public const string BaseUrl = "https://cdn.example.test/sample-app";
    private readonly ConcurrentDictionary<string, string> _files = new();

    public string Publish(string version, PlatformTarget platform, string? content = null)
    {
        var url = $"{BaseUrl}/{version}/{FeedFileName.For(platform)}";
        _files[url] = content ?? ManifestFor(version, platform);
        return url;
    }

    public Task<FetchedManifest> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!_files.TryGetValue(url.AbsoluteUri, out var content))
        {
            throw new ManifestFetchException($"Fetching {url} returned HTTP 404; redirects are not followed.");
        }
        return Task.FromResult(new FetchedManifest(url, content, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)))));
    }

    public static string FileNameFor(string version, PlatformTarget platform) => platform switch
    {
        PlatformTarget.Windows => $"Sample-App-Setup-{version}.exe",
        PlatformTarget.MacOS => $"Sample-App-{version}-mac.zip",
        _ => $"Sample-App-{version}-{platform}.AppImage",
    };

    public static string Sha512For(string version, PlatformTarget platform) =>
        Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(FileNameFor(version, platform))));

    public static string ManifestFor(string version, PlatformTarget platform) =>
        $"""
        version: {version}
        files:
          - url: {FileNameFor(version, platform)}
            sha512: {Sha512For(version, platform)}
            size: 1234567
        path: {FileNameFor(version, platform)}
        sha512: {Sha512For(version, platform)}
        releaseDate: '2026-09-30T10:00:00.000Z'
        stagingPercentage: 10

        """;
}
