using System.Net;
using Releaser.Domain.Targeting;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>
/// A node still inside its freshness window may decide to offer a release that another node has just obliterated.
/// Rendering that offer must degrade to "no update", never a 500 (PR #13 review follow-up).
/// </summary>
public sealed class ObliterationFeedRaceTests(PostgresContainer postgres)
{
    [Fact]
    public async Task a_node_with_a_stale_decision_answers_no_update_for_an_obliterated_release()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var manifests = new StubManifestHost();
        await using var nodeA = new ReleaserFactory(new FactoryOptions(connectionString, manifests, FreshnessSeconds: 30));
        await using var nodeB = new ReleaserFactory(new FactoryOptions(connectionString, manifests, FreshnessSeconds: 30));
        var admin = await nodeA.CreateAdminClient().LoginAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.2.0",
            [(PlatformTarget.Windows, manifests.Publish("1.2.0", PlatformTarget.Windows)), (PlatformTarget.MacOS, manifests.Publish("1.2.0", PlatformTarget.MacOS))]);
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        await admin.DeployAsync(app.Id, "Everyone", release.Id, everyone.Id);
        var feedB = nodeB.CreateFeedClient();
        (await feedB.CheckAsync("installation-1", "1.0.0", PlatformTarget.Windows)).IsOffer("1.2.0").ShouldBeTrue("node B caches the decision inputs");

        (await admin.RawAsync(HttpMethod.Delete, $"/api/admin/v1/applications/{app.Id}/releases/{release.Id}?confirmVersion=1.2.0", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Node B's stamp is still fresh, so it still decides "offer 1.2.0", but the macOS metadata was never rendered there.
        var mac = await feedB.CheckAsync("installation-1", "1.0.0", PlatformTarget.MacOS);
        mac.Status.ShouldBe(HttpStatusCode.OK);
        mac.FileUrls.ShouldBeEmpty();
        mac.Version.ShouldBe("1.0.0");
    }
}
