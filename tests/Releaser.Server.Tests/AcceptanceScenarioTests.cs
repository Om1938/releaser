using System.Net;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Deployments;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>
/// PRD §6 end to end over HTTP: one publisher, externally hosted 1.0.0/1.1.0/1.2.0, Customer A/B, internal QA, default audience,
/// a stable 20% rollout, pause, release notes and audit — without the platform serving any binary.
/// </summary>
public sealed class AcceptanceScenarioTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private sealed record Scenario(ApplicationResponse App, ReleaseResponse V100, ReleaseResponse V110, ReleaseResponse V120, DeploymentResponse CustomerB, ContextTokens Tokens);

    private async Task<Scenario> ArrangeAsync(AdminClient admin)
    {
        var app = await admin.CreateApplicationAsync();
        var v100 = await admin.RegisterReleaseAsync(app.Id, "1.0.0", AllPlatforms(Manifests, "1.0.0"));
        var v110 = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var v120 = await admin.RegisterReleaseAsync(app.Id, "1.2.0", AllPlatforms(Manifests, "1.2.0"));

        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        var customerA = await admin.CreateAudienceAsync(app.Id, "Customer A", new CustomerRule(["customer-a"]));
        var customerB = await admin.CreateAudienceAsync(app.Id, "Customer B", new CustomerRule(["customer-b"]));
        var qa = await admin.CreateAudienceAsync(app.Id, "Internal QA", new GroupRule(["internal-qa"]));

        await admin.DeployAsync(app.Id, "Default", v100.Id, everyone.Id);
        await admin.DeployAsync(app.Id, "Customer A", v110.Id, customerA.Id);
        var deploymentB = await admin.DeployAsync(app.Id, "Customer B", v120.Id, customerB.Id, percentage: 20m);
        await admin.DeployAsync(app.Id, "Internal QA", v120.Id, qa.Id);

        var tokens = new ContextTokens();
        await admin.RegisterContextKeyAsync(app.Id, tokens.PublicKeyPem);
        return new Scenario(app, v100, v110, v120, deploymentB, tokens);
    }

    private static IEnumerable<string> Installations(int count) => Enumerable.Range(0, count).Select(i => $"installation-{i:D5}");

    [Fact]
    public async Task targeted_versions_are_offered_per_audience()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        var feed = Node.CreateFeedClient();

        (await feed.CheckAsync("installation-a1", "1.0.0", token: scenario.Tokens.Sign("sample-app", customer: "customer-a"))).IsOffer("1.1.0").ShouldBeTrue();
        (await feed.CheckAsync("installation-q1", "1.0.0", token: scenario.Tokens.Sign("sample-app", groups: ["internal-qa"]))).IsOffer("1.2.0").ShouldBeTrue();
        (await feed.CheckAsync("installation-q2", "1.0.0", token: scenario.Tokens.Sign("sample-app", customer: "customer-a", groups: ["internal-qa"]))).IsOffer("1.2.0").ShouldBeTrue();

        var anonymous = await feed.CheckAsync("installation-d1", "1.0.0");
        anonymous.Status.ShouldBe(HttpStatusCode.OK);
        anonymous.Version.ShouldBe("1.0.0");
        anonymous.FileUrls.ShouldBeEmpty();
        (await feed.CheckAsync("installation-d2", "0.9.0")).IsOffer("1.0.0").ShouldBeTrue();
    }

    [Fact]
    public async Task customer_b_rollout_is_about_20_percent_and_stable_across_checks()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        var feed = Node.CreateFeedClient();
        var token = scenario.Tokens.Sign("sample-app", customer: "customer-b");

        async Task<HashSet<string>> OfferedAsync() =>
            [.. (await Task.WhenAll(Installations(500).Select(async id => (id, answer: await feed.CheckAsync(id, "1.0.0", token: token)))))
                .Where(r => r.answer.IsOffer("1.2.0")).Select(r => r.id)];

        var first = await OfferedAsync();
        first.Count.ShouldBeInRange(70, 130);
        (await OfferedAsync()).ShouldBe(first, ignoreOrder: true);

        await admin.ChangeRolloutAsync(scenario.App.Id, scenario.CustomerB.Id, 50m);
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var widened = await OfferedAsync();
        widened.IsSupersetOf(first).ShouldBeTrue("raising the percentage must keep earlier cohort members");
        widened.Count.ShouldBeInRange(200, 300);
    }

    [Fact]
    public async Task pausing_customer_b_stops_its_offers_within_the_freshness_bound_and_leaves_others_unchanged()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        var feed = Node.CreateFeedClient();
        var tokenB = scenario.Tokens.Sign("sample-app", customer: "customer-b");
        var cohort = (await Task.WhenAll(Installations(300).Select(async id => (id, answer: await feed.CheckAsync(id, "1.0.0", token: tokenB)))))
            .Where(r => r.answer.IsOffer("1.2.0")).Select(r => r.id).ToList();
        cohort.ShouldNotBeEmpty();

        await admin.TransitionDeploymentAsync(scenario.App.Id, scenario.CustomerB.Id, "pause");
        await Task.Delay(TimeSpan.FromSeconds(1.2)); // FreshnessSeconds = 1 in tests

        foreach (var id in cohort)
        {
            (await feed.CheckAsync(id, "1.0.0", token: tokenB)).FileUrls.ShouldBeEmpty();
        }
        (await feed.CheckAsync("installation-a1", "1.0.0", token: scenario.Tokens.Sign("sample-app", customer: "customer-a"))).IsOffer("1.1.0").ShouldBeTrue();
        (await feed.CheckAsync("installation-q1", "1.0.0", token: scenario.Tokens.Sign("sample-app", groups: ["internal-qa"]))).IsOffer("1.2.0").ShouldBeTrue();

        await admin.TransitionDeploymentAsync(scenario.App.Id, scenario.CustomerB.Id, "resume");
        await Task.Delay(TimeSpan.FromSeconds(1.2));
        (await feed.CheckAsync(cohort[0], "1.0.0", token: tokenB)).IsOffer("1.2.0").ShouldBeTrue();
    }

    [Fact]
    public async Task served_metadata_points_only_at_the_external_artifact_host_with_checksums_intact()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        var feed = Node.CreateFeedClient();
        var token = scenario.Tokens.Sign("sample-app", groups: ["internal-qa"]);

        foreach (var platform in new[] { PlatformTarget.Windows, PlatformTarget.MacOS, PlatformTarget.LinuxX64 })
        {
            var answer = await feed.CheckAsync("installation-q1", "1.0.0", platform, token);
            answer.IsOffer("1.2.0").ShouldBeTrue();
            answer.FileUrls.ShouldAllBe(url => url.StartsWith(StubManifestHost.BaseUrl + "/1.2.0/", StringComparison.Ordinal));
            answer.Body.ShouldContain($"sha512: {StubManifestHost.Sha512For("1.2.0", platform)}");
            answer.Body.ShouldNotContain("stagingPercentage");
        }
    }

    [Fact]
    public async Task applications_can_fetch_published_release_notes_and_the_feed_carries_them()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        await admin.SaveNotesAsync(scenario.App.Id, scenario.V110.Id, new SaveReleaseNoteRequest("Faster sync", "Sync is 2x faster.", "Details here.",
            [new ReleaseNoteChange(ChangeCategory.Fixed, "Crash when offline")]));

        var client = Node.CreateClient();
        var drafts = await client.GetStringAsync("/api/client/v1/apps/sample-app/notes");
        drafts.ShouldBe("[]");

        await admin.PublishNotesAsync(scenario.App.Id, scenario.V110.Id);
        var published = await client.GetStringAsync("/api/client/v1/apps/sample-app/notes?since=1.0.0");
        published.ShouldContain("\"version\":\"1.1.0\"");
        published.ShouldContain("Crash when offline");

        await Task.Delay(TimeSpan.FromSeconds(1.2));
        var answer = await Node.CreateFeedClient().CheckAsync("installation-a1", "1.0.0", token: scenario.Tokens.Sign("sample-app", customer: "customer-a"));
        answer.Body.ShouldContain("releaseNotes: |-");
        answer.Body.ShouldContain("- Crash when offline");
    }

    [Fact]
    public async Task administrative_changes_are_audited_with_actor_and_entity()
    {
        var admin = await AdminAsync();
        var scenario = await ArrangeAsync(admin);
        await admin.TransitionDeploymentAsync(scenario.App.Id, scenario.CustomerB.Id, "pause");

        var audit = await admin.AuditAsync(scenario.App.Id);
        var actions = audit.Entries.Select(e => e.Action).ToList();
        actions.ShouldContain("application.created");
        actions.Count(a => a == "release.registered").ShouldBe(3);
        actions.Count(a => a == "audience.created").ShouldBe(4);
        actions.Count(a => a == "deployment.activate").ShouldBe(4);
        actions.ShouldContain("deployment.pause");
        audit.Entries.ShouldAllBe(e => e.Actor == ReleaserFactory.AdminEmail);
        audit.Entries.Single(e => e.Action == "deployment.pause").EntityId.ShouldBe(scenario.CustomerB.Id.ToString());
    }
}
