using System.Text;
using Releaser.Domain.Common;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Releaser.Electron;

/// <summary>
/// An electron-builder update manifest (<c>latest*.yml</c>). Unknown fields are preserved verbatim;
/// only relative download locations are rewritten and checksums are never touched (ADR 0002, ADR 0003).
/// </summary>
public sealed class ElectronManifest
{
    private const int MaxLength = 1024 * 1024;
    private readonly YamlMappingNode _root;

    private ElectronManifest(YamlMappingNode root, SemanticVersion version, IReadOnlyList<ManifestFile> files)
    {
        _root = root;
        Version = version;
        Files = files;
    }

    public SemanticVersion Version { get; }
    public IReadOnlyList<ManifestFile> Files { get; }

    /// <summary>Parses and validates a manifest. Throws <see cref="DomainRuleException"/> with a <c>manifest.*</c> code when invalid.</summary>
    public static ElectronManifest Parse(string yaml)
    {
        if (yaml.Length > MaxLength)
        {
            throw Invalid("too_large", "The manifest exceeds 1 MiB.");
        }
        var root = LoadRoot(yaml);
        var version = SemanticVersion.TryParse(Scalar(root, "version"), out var parsed)
            ? parsed
            : throw Invalid("version_missing", "The manifest has no valid semantic 'version'.");
        return new ElectronManifest(root, version, ReadFiles(root));
    }

    /// <summary>Returns a copy whose relative <c>files[].url</c>, <c>path</c> and <c>packages.*.path</c> are absolute against <paramref name="manifestUrl"/>.</summary>
    public ElectronManifest WithAbsoluteUrls(Uri manifestUrl)
    {
        var copy = (YamlMappingNode)LoadRoot(ToYaml());
        var resolver = new DownloadUrlResolver(manifestUrl);
        if (copy.Children.TryGetValue("files", out var files) && files is YamlSequenceNode sequence)
        {
            foreach (var file in sequence.OfType<YamlMappingNode>())
            {
                resolver.Rewrite(file, "url");
            }
        }
        resolver.Rewrite(copy, "path");
        if (copy.Children.TryGetValue("packages", out var packages) && packages is YamlMappingNode packageMap)
        {
            foreach (var package in packageMap.Children.Values.OfType<YamlMappingNode>())
            {
                resolver.Rewrite(package, "path");
            }
        }
        return new ElectronManifest(copy, Version, ReadFiles(copy));
    }

    /// <summary>
    /// Renders the manifest served to updaters: <c>stagingPercentage</c> is removed (server-side cohorts are authoritative)
    /// and published release notes, when given, replace <c>releaseNotes</c>.
    /// </summary>
    public string RenderForFeed(string? releaseNotes)
    {
        var copy = (YamlMappingNode)LoadRoot(ToYaml());
        copy.Children.Remove(new YamlScalarNode("stagingPercentage"));
        if (releaseNotes is not null)
        {
            copy.Children[new YamlScalarNode("releaseNotes")] = new YamlScalarNode(releaseNotes) { Style = ScalarStyle.Literal };
        }
        return Serialize(copy);
    }

    public string ToYaml() => Serialize(_root);

    /// <summary>A manifest announcing the installation's own version, which electron-updater treats as "no update" (ADR 0002).</summary>
    public static string NoUpdate(SemanticVersion currentVersion) =>
        $"version: {currentVersion}\nfiles: []\n";

    private static YamlMappingNode LoadRoot(string yaml)
    {
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(yaml));
        }
        catch (YamlException exception)
        {
            throw Invalid("unparseable", $"The manifest is not valid YAML: {exception.Message}");
        }
        return stream.Documents.Count == 1 && stream.Documents[0].RootNode is YamlMappingNode root
            ? root
            : throw Invalid("not_a_mapping", "The manifest must be a single YAML mapping.");
    }

    private static List<ManifestFile> ReadFiles(YamlMappingNode root)
    {
        var files = root.Children.TryGetValue("files", out var node) && node is YamlSequenceNode sequence
            ? sequence.OfType<YamlMappingNode>().Select(file => new ManifestFile(Scalar(file, "url"), Scalar(file, "sha512"), ParseSize(Scalar(file, "size")))).ToList()
            : [];
        if (files.Count == 0 && Scalar(root, "path") is { } legacyPath)
        {
            files.Add(new ManifestFile(legacyPath, Scalar(root, "sha512"), null));
        }
        if (files.Count == 0)
        {
            throw Invalid("no_files", "The manifest lists no files.");
        }
        if (files.Any(file => string.IsNullOrWhiteSpace(file.Url)))
        {
            throw Invalid("file_url_missing", "Every manifest file needs a 'url'.");
        }
        if (files.Any(file => string.IsNullOrWhiteSpace(file.Sha512)))
        {
            throw Invalid("checksum_missing", "Every manifest file needs a 'sha512' checksum; the platform never serves unverifiable downloads.");
        }
        return files;
    }

    private static long? ParseSize(string? value) =>
        long.TryParse(value, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var size) ? size : null;

    private static string? Scalar(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(key, out var value) && value is YamlScalarNode scalar ? scalar.Value : null;

    private static string Serialize(YamlMappingNode root)
    {
        var builder = new StringBuilder();
        using var writer = new StringWriter(builder);
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        var text = builder.ToString();
        // YamlStream.Save emits an explicit document end marker; electron-builder files have none.
        return text.EndsWith("...\n", StringComparison.Ordinal) ? text[..^4] : text;
    }

    private static DomainRuleException Invalid(string code, string message) => new($"manifest.{code}", message);
}

/// <summary>One downloadable file listed in a manifest.</summary>
public sealed record ManifestFile(string? Url, string? Sha512, long? Size);
