using System.Net;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Channels;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>The electron-updater generic feed contract (ADR 0002).</summary>
public sealed class FeedProtocolTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private async Task<(AdminClient Admin, ApplicationResponse App)> ArrangeEveryoneOn110Async()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);
        return (admin, app);
    }

    [Theory]
    [InlineData("installation-1", "not-a-version", "latest.yml")]
    [InlineData("short", "1.0.0", "latest.yml")]
    [InlineData("installation-1", "1.0.0", "latest.json")]
    [InlineData("installation-1", "1.0.0", "-mac.yml")]
    public async Task malformed_feed_requests_are_rejected(string installation, string version, string file)
    {
        await ArrangeEveryoneOn110Async();
        var response = await Node.CreateClient().GetAsync($"/u/sample-app/{installation}/{version}/{file}");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task unknown_applications_and_channels_return_404()
    {
        await ArrangeEveryoneOn110Async();
        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.0.0", appKey: "other-app")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await feed.CheckAsync("installation-1", "1.0.0", electronChannel: "nightly")).Status.ShouldBe(HttpStatusCode.OK);
        (await feed.CheckAsync("installation-1", "1.0.0", electronChannel: "nightly")).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task each_platform_file_gets_its_own_manifest()
    {
        await ArrangeEveryoneOn110Async();
        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.0.0", PlatformTarget.Windows)).FileUrls.ShouldBe([$"{StubManifestHost.BaseUrl}/1.1.0/Sample-App-Setup-1.1.0.exe"]);
        (await feed.CheckAsync("installation-1", "1.0.0", PlatformTarget.MacOS)).FileUrls.ShouldBe([$"{StubManifestHost.BaseUrl}/1.1.0/Sample-App-1.1.0-mac.zip"]);
        (await feed.CheckAsync("installation-1", "1.0.0", PlatformTarget.LinuxX64)).FileUrls.ShouldBe([$"{StubManifestHost.BaseUrl}/1.1.0/Sample-App-1.1.0-LinuxX64.AppImage"]);
    }

    [Fact]
    public async Task a_platform_without_a_registered_manifest_gets_no_update()
    {
        await ArrangeEveryoneOn110Async();
        var answer = await Node.CreateFeedClient().CheckAsync("installation-1", "1.0.0", PlatformTarget.LinuxArm64);
        answer.Status.ShouldBe(HttpStatusCode.OK);
        answer.Body.ShouldBe("version: 1.0.0\nfiles: []\n");
    }

    [Fact]
    public async Task an_installation_on_a_newer_version_receives_its_own_version_so_it_never_downgrades()
    {
        await ArrangeEveryoneOn110Async();
        var answer = await Node.CreateFeedClient().CheckAsync("installation-1", "2.0.0");
        answer.Version.ShouldBe("2.0.0");
        answer.FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task feed_responses_are_not_cacheable_yaml()
    {
        await ArrangeEveryoneOn110Async();
        var response = await Node.CreateClient().GetAsync("/u/sample-app/installation-1/1.0.0/latest.yml");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/yaml");
    }

    [Fact]
    public async Task custom_electron_channels_resolve_against_platform_channels()
    {
        var (admin, app) = await ArrangeEveryoneOn110Async();
        await admin.SendAsync<ChannelResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/channels", new CreateChannelRequest("beta", "Beta", null));
        var beta = await admin.RegisterReleaseAsync(app.Id, "1.2.0-beta.1", AllPlatforms(Manifests, "1.2.0-beta.1"), "beta");
        var everyone = (await admin.SendAsync<List<Features.Audiences.AudienceResponse>>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/audiences", null)).Single();
        await admin.DeployAsync(app.Id, "Beta testers", beta.Id, everyone.Id, channel: "beta");

        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.1.0", PlatformTarget.MacOS, electronChannel: "beta")).IsOffer("1.2.0-beta.1").ShouldBeTrue();
        (await feed.CheckAsync("installation-1", "1.1.0", PlatformTarget.MacOS)).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task no_endpoint_serves_binaries_for_artifact_paths()
    {
        await ArrangeEveryoneOn110Async();
        var client = Node.CreateClient();
        (await client.GetAsync("/u/sample-app/installation-1/1.0.0/Sample-App-Setup-1.1.0.exe")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/u/sample-app/installation-1/1.0.0/Sample-App-Setup-1.1.0.exe.blockmap")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAsync("/u/sample-app/1.1.0/Sample-App-Setup-1.1.0.exe")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
