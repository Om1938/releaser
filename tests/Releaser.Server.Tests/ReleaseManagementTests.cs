using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Releaser.Domain.Policies;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Releases;
using Releaser.Server.Infrastructure.Persistence;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Release registration, immutability, lifecycle and policies through the API and PostgreSQL.</summary>
public sealed class ReleaseManagementTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    [Fact]
    public async Task registering_snapshots_manifests_with_absolute_external_urls()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.0.0", AllPlatforms(Manifests, "1.0.0"));

        release.State.ShouldBe(Domain.Releases.ReleaseState.Available);
        release.Platforms.ShouldBe([PlatformTarget.Windows, PlatformTarget.MacOS, PlatformTarget.LinuxX64]);
        release.Manifests.SelectMany(m => m.Files).ShouldAllBe(f => f.Url!.StartsWith(StubManifestHost.BaseUrl, StringComparison.Ordinal));
        release.Manifests.Single(m => m.Platform == PlatformTarget.MacOS).FeedFileName.ShouldBe("latest-mac.yml");
    }

    [Fact]
    public async Task a_version_can_be_registered_only_once()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        await admin.RegisterReleaseAsync(app.Id, "1.0.0", AllPlatforms(Manifests, "1.0.0"));
        var duplicate = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.0.0", null, ["stable"], [new ManifestReference(PlatformTarget.Windows, Manifests.Publish("1.0.0", PlatformTarget.Windows))]));
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task a_manifest_declaring_another_version_is_rejected()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var url = Manifests.Publish("1.3.0", PlatformTarget.Windows, StubManifestHost.ManifestFor("1.2.0", PlatformTarget.Windows));
        var response = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.3.0", null, ["stable"], [new ManifestReference(PlatformTarget.Windows, url)]));
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("manifest.version_mismatch");
    }

    [Fact]
    public async Task manifests_without_checksums_or_unreachable_ones_are_rejected()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var noChecksum = Manifests.Publish("1.4.0", PlatformTarget.Windows, "version: 1.4.0\nfiles:\n  - url: a.exe\n");
        var unsigned = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.4.0", null, ["stable"], [new ManifestReference(PlatformTarget.Windows, noChecksum)]));
        (await unsigned.Content.ReadAsStringAsync()).ShouldContain("manifest.checksum_missing");

        var missing = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.5.0", null, ["stable"], [new ManifestReference(PlatformTarget.Windows, $"{StubManifestHost.BaseUrl}/nope/latest.yml")]));
        missing.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task registering_without_deploying_offers_nothing()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        (await Node.CreateFeedClient().CheckAsync("installation-1", "1.0.0")).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task withdrawing_a_release_stops_offers_and_cannot_be_undone()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);

        await admin.SendAsync<ReleaseResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}/withdraw", null);
        (await Node.CreateFeedClient().CheckAsync("installation-1", "1.0.0")).FileUrls.ShouldBeEmpty();
        (await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}/withdraw", null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/deployments",
            new Features.Deployments.CreateDeploymentRequest("again", release.Id, "stable", everyone.Id, 100, 0))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task promoting_to_another_channel_needs_no_rebuild()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        await admin.SendAsync<Features.Channels.ChannelResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/channels", new Features.Channels.CreateChannelRequest("beta", "Beta", null));
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"), "beta");
        var promoted = await admin.SendAsync<ReleaseResponse>(HttpMethod.Put, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}/channels", new AssignChannelsRequest(["beta", "stable"]));
        promoted.Channels.ShouldBe(["beta", "stable"], ignoreOrder: true);
        var reloaded = await admin.SendAsync<ReleaseResponse>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}", null);
        reloaded.Channels.ShouldBe(["beta", "stable"], ignoreOrder: true);
        promoted.Manifests.Select(m => m.SourceSha256).ShouldBe(release.Manifests.Select(m => m.SourceSha256));
    }

    [Fact]
    public async Task pins_and_exclusions_apply_through_the_feed()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var v110 = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var v120 = await admin.RegisterReleaseAsync(app.Id, "1.2.0", AllPlatforms(Manifests, "1.2.0"));
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        var held = await admin.CreateAudienceAsync(app.Id, "Held installation", new InstallationRule(["installation-held"]));
        var broken = await admin.CreateAudienceAsync(app.Id, "Broken hardware", new InstallationRule(["installation-broken"]));
        await admin.DeployAsync(app.Id, "Everyone", v120.Id, everyone.Id);
        await admin.CreatePolicyAsync(app.Id, "Hold on 1.1.0", PolicyKind.Pin, held.Id, v110.Id);
        await admin.CreatePolicyAsync(app.Id, "Skip 1.2.0", PolicyKind.Exclusion, broken.Id, v120.Id);

        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-held", "1.0.0")).IsOffer("1.1.0").ShouldBeTrue();
        (await feed.CheckAsync("installation-broken", "1.0.0")).FileUrls.ShouldBeEmpty();
        (await feed.CheckAsync("installation-other", "1.0.0")).IsOffer("1.2.0").ShouldBeTrue();
    }

    [Fact]
    public async Task each_committed_change_increments_the_configuration_version_atomically_with_its_audit_entry()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        long Version() => Scope().Applications.AsNoTracking().Single(a => a.Id == new Domain.Common.AppId(app.Id)).ConfigVersion;
        var before = Version();
        await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        Version().ShouldBe(before + 1);
        Scope().AuditEntries.Count(e => e.AppId == app.Id).ShouldBe(2);
    }

    [Fact]
    public async Task migrations_match_the_model()
    {
        var db = Scope();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    private ReleaserDbContext Scope() => Node.Services.CreateScope().ServiceProvider.GetRequiredService<ReleaserDbContext>();
}
