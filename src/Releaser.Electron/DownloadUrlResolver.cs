using YamlDotNet.RepresentationModel;

namespace Releaser.Electron;

/// <summary>
/// Resolves relative download locations exactly as electron-updater would against the original manifest location:
/// <c>new URL(path, base)</c>, then the base query string (if any) replaces the result's query.
/// Absolute locations are left byte-identical.
/// </summary>
internal sealed class DownloadUrlResolver(Uri manifestUrl)
{
    public void Rewrite(YamlMappingNode node, string key)
    {
        if (node.Children.TryGetValue(key, out var value)
            && value is YamlScalarNode { Value: { } location } scalar
            && !Uri.IsWellFormedUriString(location, UriKind.Absolute))
        {
            scalar.Value = Resolve(location);
        }
    }

    public string Resolve(string location)
    {
        var builder = new UriBuilder(new Uri(manifestUrl, location));
        if (!string.IsNullOrEmpty(manifestUrl.Query))
        {
            builder.Query = manifestUrl.Query.TrimStart('?');
        }
        return builder.Uri.AbsoluteUri;
    }
}
