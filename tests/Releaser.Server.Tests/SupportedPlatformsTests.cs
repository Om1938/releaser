using System.Net;
using Releaser.Domain.Resolution;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Decisions;
using Releaser.Server.Features.Releases;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Issue #10: each application declares the platforms it ships to.</summary>
public sealed class SupportedPlatformsTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private static readonly PlatformTarget[] WindowsAndMac = [PlatformTarget.Windows, PlatformTarget.MacOS];

    [Fact]
    public async Task an_application_needs_at_least_one_platform()
    {
        var admin = await AdminAsync();
        var response = await admin.RawAsync(HttpMethod.Post, "/api/admin/v1/applications",
            new CreateApplicationRequest("no-platforms", "No platforms", null, "stable", "Stable", []));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task the_application_reports_its_supported_platforms()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync(platforms: [PlatformTarget.MacOS, PlatformTarget.Windows]);
        app.SupportedPlatforms.ShouldBe(WindowsAndMac);
    }

    [Fact]
    public async Task unsupported_platforms_cannot_be_registered_or_added()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync(platforms: WindowsAndMac);

        var registering = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.1.0", null, ["stable"],
                [new ManifestReference(PlatformTarget.LinuxX64, Manifests.Publish("1.1.0", PlatformTarget.LinuxX64))]));
        registering.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await registering.Content.ReadAsStringAsync()).ShouldContain("release.platform_not_supported");

        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", [(PlatformTarget.Windows, Manifests.Publish("1.1.0", PlatformTarget.Windows))]);
        var adding = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}/manifests",
            new ManifestReference(PlatformTarget.LinuxX64, Manifests.Publish("1.1.0", PlatformTarget.LinuxX64)));
        adding.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await adding.Content.ReadAsStringAsync()).ShouldContain("release.platform_not_supported");
    }

    [Fact]
    public async Task dropping_a_platform_stops_its_offers_and_leaves_others_unchanged()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync(platforms: WindowsAndMac);
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0",
            [(PlatformTarget.Windows, Manifests.Publish("1.1.0", PlatformTarget.Windows)), (PlatformTarget.MacOS, Manifests.Publish("1.1.0", PlatformTarget.MacOS))]);
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);
        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-mac", "1.0.0", PlatformTarget.MacOS)).IsOffer("1.1.0").ShouldBeTrue();

        var updated = await admin.SendAsync<ApplicationResponse>(HttpMethod.Put, $"/api/admin/v1/applications/{app.Id}/supported-platforms",
            new ChangeSupportedPlatformsRequest([PlatformTarget.Windows]));
        updated.SupportedPlatforms.ShouldBe([PlatformTarget.Windows]);

        (await feed.CheckAsync("installation-mac", "1.0.0", PlatformTarget.MacOS)).FileUrls.ShouldBeEmpty();
        (await feed.CheckAsync("installation-win", "1.0.0", PlatformTarget.Windows)).IsOffer("1.1.0").ShouldBeTrue();
        var explained = await admin.SendAsync<ExplainDecisionResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/decisions/explain",
            new ExplainDecisionRequest(null, PlatformTarget.MacOS, "1.0.0", "installation-mac", null, null, null, null));
        explained.Reason.ShouldBe(DecisionReason.PlatformNotSupported);
        (await admin.AuditAsync(app.Id)).Entries.ShouldContain(e => e.Action == "application.platforms_changed" && e.Details == "Windows,MacOS -> Windows");
    }

    [Fact]
    public async Task renaming_the_application_never_touches_its_platforms()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync(platforms: WindowsAndMac);
        var renamed = await admin.SendAsync<ApplicationResponse>(HttpMethod.Put, $"/api/admin/v1/applications/{app.Id}",
            new UpdateApplicationRequest("Renamed", null, app.DefaultChannel));
        renamed.Name.ShouldBe("Renamed");
        renamed.SupportedPlatforms.ShouldBe(WindowsAndMac);
    }
}
