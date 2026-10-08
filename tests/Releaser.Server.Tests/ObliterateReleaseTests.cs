using System.Net;
using Releaser.Domain.Policies;
using Releaser.Domain.ReleaseNotes;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Deployments;
using Releaser.Server.Features.Policies;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Issue #12: permanently delete a release so its version can be registered again.</summary>
public sealed class ObliterateReleaseTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private sealed record Arranged(AdminClient Admin, ApplicationResponse App, ReleaseResponse Kept, ReleaseResponse Doomed);

    /// <summary>1.1.0 and 1.2.0 deployed to everyone (1.2.0 wins on version); 1.2.0 also has a draft deployment, a pin, an exclusion and published notes.</summary>
    private async Task<Arranged> ArrangeAsync()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var kept = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var doomed = await admin.RegisterReleaseAsync(app.Id, "1.2.0", AllPlatforms(Manifests, "1.2.0"));
        var everyone = await admin.CreateAudienceAsync(app.Id, "Everyone", new EveryoneRule());
        var qa = await admin.CreateAudienceAsync(app.Id, "QA", new GroupRule(["qa"]));
        await admin.DeployAsync(app.Id, "Everyone 1.1.0", kept.Id, everyone.Id);
        await admin.DeployAsync(app.Id, "Everyone 1.2.0", doomed.Id, everyone.Id);
        await admin.CreateDeploymentAsync(app.Id, "QA draft 1.2.0", doomed.Id, qa.Id);
        await admin.CreatePolicyAsync(app.Id, "Pin QA", PolicyKind.Pin, qa.Id, doomed.Id);
        var broken = await admin.CreateAudienceAsync(app.Id, "Broken installation", new InstallationRule(["installation-broken"]));
        await admin.CreatePolicyAsync(app.Id, "Skip for installation", PolicyKind.Exclusion, broken.Id, doomed.Id);
        await admin.SaveNotesAsync(app.Id, doomed.Id, new SaveReleaseNoteRequest("Oops", null, "", [new ReleaseNoteChange(ChangeCategory.Fixed, "x")]));
        await admin.PublishNotesAsync(app.Id, doomed.Id);
        return new Arranged(admin, app, kept, doomed);
    }

    private static Task<HttpResponseMessage> ObliterateAsync(AdminClient admin, Guid appId, Guid releaseId, string? confirm) =>
        admin.RawAsync(HttpMethod.Delete, $"/api/admin/v1/applications/{appId}/releases/{releaseId}" + (confirm is null ? "" : $"?confirmVersion={confirm}"), null);

    [Fact]
    public async Task the_impact_preview_lists_everything_that_will_be_deleted()
    {
        var (admin, app, _, doomed) = await ArrangeAsync();
        var impact = await admin.SendAsync<ObliterationImpact>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{doomed.Id}/obliteration-impact", null);
        impact.Version.ShouldBe("1.2.0");
        impact.Deployments.Count.ShouldBe(2);
        impact.Deployments.Single(d => d.Name == "Everyone 1.2.0").IsLive.ShouldBeTrue();
        impact.Deployments.Single(d => d.Name == "QA draft 1.2.0").IsLive.ShouldBeFalse();
        impact.Pins.ShouldBe(1);
        impact.Exclusions.ShouldBe(1);
        impact.HasReleaseNote.ShouldBeTrue();
        impact.ReleaseNoteRevisions.ShouldBe(2);
    }

    [Fact]
    public async Task obliterating_removes_the_release_and_everything_attached_and_frees_the_version()
    {
        var (admin, app, kept, doomed) = await ArrangeAsync();
        var feed = Node.CreateFeedClient();
        (await feed.CheckAsync("installation-1", "1.0.0")).IsOffer("1.2.0").ShouldBeTrue();
        var duplicate = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/releases",
            new RegisterReleaseRequest("1.2.0", null, ["stable"], [new ManifestReference(PlatformTarget.Windows, Manifests.Publish("1.2.0", PlatformTarget.Windows))]));
        (await duplicate.Content.ReadAsStringAsync()).ShouldContain("obliterate");

        (await ObliterateAsync(admin, app.Id, doomed.Id, "1.2.0")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.RawAsync(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{doomed.Id}", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var deployments = await admin.SendAsync<List<DeploymentResponse>>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/deployments", null);
        deployments.ShouldAllBe(d => d.ReleaseId == kept.Id);
        (await admin.SendAsync<List<PolicyResponse>>(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/policies", null)).ShouldBeEmpty();
        (await Node.CreateClient().GetStringAsync("/api/client/v1/apps/sample-app/notes")).ShouldBe("[]");
        (await feed.CheckAsync("installation-1", "1.0.0")).IsOffer("1.1.0").ShouldBeTrue("the remaining deployment takes over immediately on the writing node");

        var again = await admin.RegisterReleaseAsync(app.Id, "1.2.0", [(PlatformTarget.Windows, Manifests.Publish("1.2.0", PlatformTarget.Windows))]);
        again.Id.ShouldNotBe(doomed.Id);

        var entry = (await admin.AuditAsync(app.Id)).Entries.Single(e => e.Action == "release.obliterated");
        entry.EntityId.ShouldBe(doomed.Id.ToString());
        var details = entry.Details.ShouldNotBeNull();
        details.ShouldContain("version=1.2.0");
        details.ShouldContain($"{StubManifestHost.BaseUrl}/1.2.0/latest-mac.yml#sha256:");
        details.ShouldContain("deployments=2");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1.2")]
    [InlineData("1.1.0")]
    public async Task a_wrong_or_missing_confirmation_deletes_nothing(string? confirm)
    {
        var (admin, app, _, doomed) = await ArrangeAsync();
        var response = await ObliterateAsync(admin, app.Id, doomed.Id, confirm);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("release.obliteration_unconfirmed");
        (await admin.RawAsync(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{doomed.Id}", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(AdminRoles.ReleaseManager)]
    [InlineData(AdminRoles.Viewer)]
    public async Task only_admins_can_obliterate(string role)
    {
        var (admin, app, _, doomed) = await ArrangeAsync();
        var email = $"{role.ToLowerInvariant()}@example.test";
        await admin.CreateUserAsync(email, role);
        var other = await Node.CreateAdminClient().LoginAsync(email);

        (await ObliterateAsync(other, app.Id, doomed.Id, "1.2.0")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.RawAsync(HttpMethod.Get, $"/api/admin/v1/applications/{app.Id}/releases/{doomed.Id}/obliteration-impact", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task unknown_releases_return_404()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        (await ObliterateAsync(admin, app.Id, Guid.NewGuid(), "1.0.0")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
