using System.Net;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.Releases;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Issue #8: register a release for some platforms now and add other platforms' manifests later.</summary>
public sealed class AddPlatformLaterTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private async Task<(AdminClient Admin, ApplicationResponse App, ReleaseResponse Release)> ArrangeWindowsOnlyAsync()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", [(PlatformTarget.Windows, Manifests.Publish("1.1.0", PlatformTarget.Windows))]);
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);
        return (admin, app, release);
    }

    private static Task<HttpResponseMessage> AddAsync(AdminClient admin, Guid appId, Guid releaseId, PlatformTarget platform, string url) =>
        admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/releases/{releaseId}/manifests", new ManifestReference(platform, url));

    [Fact]
    public async Task a_windows_only_release_offers_nothing_to_macos_until_the_mac_manifest_is_added()
    {
        var (admin, app, release) = await ArrangeWindowsOnlyAsync();
        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-mac", "1.0.0", PlatformTarget.MacOS)).FileUrls.ShouldBeEmpty();

        var response = await AddAsync(admin, app.Id, release.Id, PlatformTarget.MacOS, Manifests.Publish("1.1.0", PlatformTarget.MacOS));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var mac = await feed.CheckAsync("installation-mac", "1.0.0", PlatformTarget.MacOS);
        mac.IsOffer("1.1.0").ShouldBeTrue();
        mac.FileUrls.ShouldBe([$"{StubManifestHost.BaseUrl}/1.1.0/Sample-App-1.1.0-mac.zip"]);
        (await feed.CheckAsync("installation-win", "1.0.0", PlatformTarget.Windows)).IsOffer("1.1.0").ShouldBeTrue();

        var reloaded = await admin.SendAsync<ReleaseResponse>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}", null);
        reloaded.Platforms.ShouldBe([PlatformTarget.Windows, PlatformTarget.MacOS]);
        reloaded.Manifests.Select(m => m.Platform).ShouldBe([PlatformTarget.Windows, PlatformTarget.MacOS]);
        (await admin.AuditAsync(app.Id)).Entries.ShouldContain(e => e.Action == "release.manifest_added" && e.Details!.Contains("platform=MacOS"));
    }

    [Fact]
    public async Task registered_manifests_cannot_be_replaced()
    {
        var (admin, app, release) = await ArrangeWindowsOnlyAsync();
        var response = await AddAsync(admin, app.Id, release.Id, PlatformTarget.Windows, Manifests.Publish("1.1.0", PlatformTarget.Windows));
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("release.platform_exists");
    }

    [Fact]
    public async Task a_withdrawn_release_cannot_gain_platforms()
    {
        var (admin, app, release) = await ArrangeWindowsOnlyAsync();
        await admin.SendAsync<ReleaseResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}/withdraw", null);
        var response = await AddAsync(admin, app.Id, release.Id, PlatformTarget.MacOS, Manifests.Publish("1.1.0", PlatformTarget.MacOS));
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("release.withdrawn");
    }

    [Fact]
    public async Task an_added_manifest_must_declare_the_release_version()
    {
        var (admin, app, release) = await ArrangeWindowsOnlyAsync();
        var url = Manifests.Publish("1.1.0", PlatformTarget.MacOS, StubManifestHost.ManifestFor("1.2.0", PlatformTarget.MacOS));
        var response = await AddAsync(admin, app.Id, release.Id, PlatformTarget.MacOS, url);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("manifest.version_mismatch");

        var reloaded = await admin.SendAsync<ReleaseResponse>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}", null);
        reloaded.Platforms.ShouldBe([PlatformTarget.Windows]);
    }

    [Fact]
    public async Task fetch_failures_name_the_platform()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var response = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.1.0", null, ["stable"],
            [
                new ManifestReference(PlatformTarget.Windows, Manifests.Publish("1.1.0", PlatformTarget.Windows)),
                new ManifestReference(PlatformTarget.MacOS, $"{StubManifestHost.BaseUrl}/1.1.0/latest-mac.yml"),
            ]));
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).ShouldContain("MacOS manifest: Fetching");
    }
}
