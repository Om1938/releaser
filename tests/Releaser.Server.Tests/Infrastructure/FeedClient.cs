using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Releaser.Domain.Targeting;
using Releaser.Electron;

namespace Releaser.Server.Tests.Infrastructure;

public sealed record FeedAnswer(HttpStatusCode Status, string Body, string? Version, IReadOnlyList<string> FileUrls)
{
    public bool IsOffer(string version) => Status == HttpStatusCode.OK && Version == version && FileUrls.Count > 0;
}

/// <summary>Requests the feed exactly as electron-updater's GenericProvider would.</summary>
public sealed partial class FeedClient(HttpClient http)
{
    public async Task<FeedAnswer> CheckAsync(string installationId, string currentVersion, PlatformTarget platform = PlatformTarget.Windows,
        string? token = null, string appKey = "sample-app", string electronChannel = "latest")
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/u/{appKey}/{installationId}/{currentVersion}/{FeedFileName.For(platform, electronChannel)}");
        request.Headers.UserAgent.ParseAdd("electron-builder");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        var response = await http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var version = VersionLine().Match(body) is { Success: true } match ? match.Groups[1].Value : null;
        var files = FileUrl().Matches(body).Select(m => m.Groups[1].Value).ToList();
        return new FeedAnswer(response.StatusCode, body, version, files);
    }

    [GeneratedRegex(@"^version: (\S+)$", RegexOptions.Multiline)]
    private static partial Regex VersionLine();

    [GeneratedRegex(@"^- url: (\S+)$", RegexOptions.Multiline)]
    private static partial Regex FileUrl();
}
