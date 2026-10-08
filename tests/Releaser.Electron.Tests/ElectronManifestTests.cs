using Releaser.Domain.Common;

namespace Releaser.Electron.Tests;

public sealed class ElectronManifestTests
{
    private static readonly Uri ManifestUrl = new("https://cdn.example.com/sample-app/1.2.0/latest.yml");

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Theory]
    [InlineData("latest.yml", 1)]
    [InlineData("latest-mac.yml", 3)]
    [InlineData("latest-linux.yml", 1)]
    [InlineData("latest-linux-arm64.yml", 1)]
    [InlineData("nsis-web.yml", 1)]
    public void parses_electron_builder_manifests(string fixture, int fileCount)
    {
        var manifest = ElectronManifest.Parse(Fixture(fixture));
        manifest.Version.ShouldBe(SemanticVersion.Parse("1.2.0"));
        manifest.Files.Count.ShouldBe(fileCount);
    }

    [Fact]
    public void relative_file_urls_become_absolute_against_the_manifest_location()
    {
        var manifest = ElectronManifest.Parse(Fixture("latest-mac.yml")).WithAbsoluteUrls(ManifestUrl);

        manifest.Files.Select(f => f.Url).ShouldBe([
            "https://cdn.example.com/sample-app/1.2.0/Sample-App-1.2.0-arm64-mac.zip",
            "https://cdn.example.com/sample-app/1.2.0/Sample-App-1.2.0-mac.zip",
            "https://cdn.example.com/sample-app/1.2.0/Sample-App-1.2.0-arm64.dmg",
        ]);
        manifest.ToYaml().ShouldContain("path: https://cdn.example.com/sample-app/1.2.0/Sample-App-1.2.0-arm64-mac.zip");
    }

    [Fact]
    public void checksums_sizes_and_other_fields_are_preserved_exactly()
    {
        var original = ElectronManifest.Parse(Fixture("latest-linux.yml"));
        var rewritten = original.WithAbsoluteUrls(ManifestUrl);

        rewritten.Files.Select(f => (f.Sha512, f.Size)).ShouldBe(original.Files.Select(f => (f.Sha512, f.Size)));
        var yaml = rewritten.ToYaml();
        yaml.ShouldContain("blockMapSize: 116000");
        yaml.ShouldContain("releaseDate: '2026-09-30T10:00:00.000Z'");
        yaml.ShouldContain("sha512: TGludXhBcHBJbWFnZVNoYTUxMkNoZWNrc3VtRm9yVGVzdGluZw==");
    }

    [Fact]
    public void absolute_urls_are_left_byte_identical()
    {
        var yaml = ElectronManifest.Parse(Fixture("latest-linux-arm64.yml")).WithAbsoluteUrls(ManifestUrl).ToYaml();
        yaml.ShouldContain("url: https://downloads.example.com/sample-app/1.2.0/Sample-App-1.2.0-arm64.AppImage?token=abc%20def");
    }

    [Fact]
    public void nsis_web_package_paths_are_rewritten_too()
    {
        var yaml = ElectronManifest.Parse(Fixture("nsis-web.yml")).WithAbsoluteUrls(ManifestUrl).ToYaml();
        yaml.ShouldContain("path: https://cdn.example.com/sample-app/1.2.0/sample-app-1.2.0-x64.nsis.7z");
        yaml.ShouldContain("path: https://cdn.example.com/sample-app/1.2.0/sample-app-1.2.0-arm64.nsis.7z");
    }

    [Fact]
    public void a_query_string_on_the_manifest_url_is_carried_to_relative_files_like_electron_updater_does()
    {
        var manifest = ElectronManifest.Parse(Fixture("latest.yml"))
            .WithAbsoluteUrls(new Uri("https://cdn.example.com/sample-app/1.2.0/latest.yml?sig=xyz"));
        manifest.Files[0].Url.ShouldBe("https://cdn.example.com/sample-app/1.2.0/Sample-App-Setup-1.2.0.exe?sig=xyz");
    }

    [Fact]
    public void url_paths_with_spaces_are_percent_encoded_like_whatwg_url()
    {
        var manifest = ElectronManifest.Parse("version: 1.0.0\nfiles:\n  - url: Sample App Setup 1.0.0.exe\n    sha512: abc\n")
            .WithAbsoluteUrls(ManifestUrl);
        manifest.Files[0].Url.ShouldBe("https://cdn.example.com/sample-app/1.2.0/Sample%20App%20Setup%201.0.0.exe");
    }

    [Fact]
    public void feed_rendering_drops_staging_percentage_and_injects_release_notes()
    {
        var rendered = ElectronManifest.Parse(Fixture("latest-linux.yml")).RenderForFeed("## Fixes\n- Crash on start");

        rendered.ShouldNotContain("stagingPercentage");
        rendered.ShouldContain("releaseNotes: |-");
        rendered.ShouldContain("  ## Fixes");
    }

    [Fact]
    public void feed_rendering_without_notes_keeps_existing_release_notes()
    {
        var rendered = ElectronManifest.Parse("version: 1.0.0\nreleaseNotes: original\nfiles:\n  - url: a.exe\n    sha512: abc\n").RenderForFeed(null);
        rendered.ShouldContain("releaseNotes: original");
    }

    [Theory]
    [InlineData("files:\n  - url: a.exe\n    sha512: abc\n", "manifest.version_missing")]
    [InlineData("version: 1.0.0\nfiles: []\n", "manifest.no_files")]
    [InlineData("version: 1.0.0\nfiles:\n  - url: a.exe\n", "manifest.checksum_missing")]
    [InlineData("version: 1.0.0\nfiles:\n  - sha512: abc\n", "manifest.file_url_missing")]
    [InlineData("- just\n- a list\n", "manifest.not_a_mapping")]
    [InlineData("version: [unclosed\n", "manifest.unparseable")]
    public void invalid_manifests_are_rejected_with_a_specific_code(string yaml, string code)
    {
        Should.Throw<DomainRuleException>(() => ElectronManifest.Parse(yaml)).Code.ShouldBe(code);
    }

    [Fact]
    public void no_update_manifest_announces_the_current_version()
    {
        ElectronManifest.NoUpdate(SemanticVersion.Parse("1.1.0")).ShouldBe("version: 1.1.0\nfiles: []\n");
    }
}
