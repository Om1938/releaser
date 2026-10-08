using System.Net;
using System.Net.Http.Json;
using Releaser.Server.Features.Admin;
using Releaser.Server.Features.Applications;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Administrative access is separated from updater consumption and scoped by role (ADR 0009).</summary>
public sealed class PermissionTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private static readonly CreateApplicationRequest NewApp = new("other-app", "Other", null, "stable", "Stable");

    private async Task<AdminClient> SignInAsAsync(string role)
    {
        var admin = await AdminAsync();
        var email = $"{role.ToLowerInvariant()}@example.test";
        await admin.CreateUserAsync(email, role);
        return await Node.CreateAdminClient().LoginAsync(email);
    }

    [Theory]
    [InlineData("/api/admin/v1/applications")]
    [InlineData("/api/admin/v1/audit")]
    [InlineData("/api/admin/v1/users")]
    public async Task anonymous_callers_cannot_read_the_admin_api(string path)
    {
        (await Node.CreateAdminClient().Http.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task viewers_can_read_but_not_change_anything()
    {
        var viewer = await SignInAsAsync(AdminRoles.Viewer);
        (await viewer.RawAsync(HttpMethod.Get, "/api/admin/v1/applications", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await viewer.RawAsync(HttpMethod.Post, "/api/admin/v1/applications", NewApp)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await viewer.RawAsync(HttpMethod.Get, "/api/admin/v1/users", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task release_managers_manage_releases_but_not_users()
    {
        var manager = await SignInAsAsync(AdminRoles.ReleaseManager);
        (await manager.RawAsync(HttpMethod.Post, "/api/admin/v1/applications", NewApp)).StatusCode.ShouldBe(HttpStatusCode.Created);
        (await manager.RawAsync(HttpMethod.Post, "/api/admin/v1/users", new CreateUserRequest("x@example.test", "X", ReleaserFactory.AdminPassword, AdminRoles.Admin)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task state_changes_without_the_csrf_header_are_rejected()
    {
        await AdminAsync();
        var client = Node.CreateClient(new() { HandleCookies = true, BaseAddress = new Uri("https://localhost") });
        (await client.PostAsJsonAsync("/api/admin/v1/auth/login", new LoginRequest(ReleaserFactory.AdminEmail, ReleaserFactory.AdminPassword), AdminClient.Json))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task wrong_passwords_are_rejected_and_accounts_lock_out()
    {
        await AdminAsync();
        var client = Node.CreateAdminClient();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await client.RawAsync(HttpMethod.Post, "/api/admin/v1/auth/login", new LoginRequest(ReleaserFactory.AdminEmail, "wrong-password-123")))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        var locked = await client.RawAsync(HttpMethod.Post, "/api/admin/v1/auth/login", new LoginRequest(ReleaserFactory.AdminEmail, ReleaserFactory.AdminPassword));
        locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await locked.Content.ReadAsStringAsync()).ShouldContain("locked");
    }

    [Fact]
    public async Task the_admin_session_cookie_does_not_grant_feed_internals_and_feed_needs_no_admin_auth()
    {
        var admin = await AdminAsync();
        await admin.CreateApplicationAsync();
        (await Node.CreateFeedClient().CheckAsync("installation-1", "1.0.0")).Status.ShouldBe(HttpStatusCode.OK);
        (await Node.CreateClient().GetAsync("/api/client/v1/apps/sample-app/notes")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task the_last_administrator_cannot_be_demoted()
    {
        var admin = await AdminAsync();
        var me = await admin.SendAsync<CurrentUserResponse>(HttpMethod.Get, "/api/admin/v1/auth/me", null);
        var response = await admin.RawAsync(HttpMethod.Put, $"/api/admin/v1/users/{me.Id}", new UpdateUserRequest("Admin", AdminRoles.Viewer));
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
