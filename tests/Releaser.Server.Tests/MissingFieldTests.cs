using System.Net;
using System.Net.Http.Json;
using Releaser.Domain.Targeting;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Omitted (null) collection fields are validation errors (400), never unexpected errors (500). PR #11 review.</summary>
public sealed class MissingFieldTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private static async Task<HttpStatusCode> SendJsonAsync(AdminClient admin, HttpMethod method, string path, string json)
    {
        using var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        return (await admin.Http.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task requests_missing_collection_fields_are_rejected_with_400()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.0.0", [(PlatformTarget.Windows, Manifests.Publish("1.0.0", PlatformTarget.Windows))]);
        var basePath = $"/api/admin/v1/applications/{app.Id}";

        (await SendJsonAsync(admin, HttpMethod.Post, "/api/admin/v1/applications",
            """{"key":"no-platforms","name":"X","defaultChannelKey":"stable","defaultChannelName":"Stable"}""")).ShouldBe(HttpStatusCode.BadRequest);
        (await SendJsonAsync(admin, HttpMethod.Put, $"{basePath}/supported-platforms", "{}")).ShouldBe(HttpStatusCode.BadRequest);
        (await SendJsonAsync(admin, HttpMethod.Post, $"{basePath}/releases", """{"version":"1.1.0","channels":["stable"]}""")).ShouldBe(HttpStatusCode.BadRequest);
        (await SendJsonAsync(admin, HttpMethod.Put, $"{basePath}/releases/{release.Id}/channels", "{}")).ShouldBe(HttpStatusCode.BadRequest);
        (await SendJsonAsync(admin, HttpMethod.Put, $"{basePath}/releases/{release.Id}/notes", """{"title":"T","bodyMarkdown":""}""")).ShouldBe(HttpStatusCode.BadRequest);
        (await SendJsonAsync(admin, HttpMethod.Post, $"{basePath}/audiences", """{"name":"A"}""")).ShouldBe(HttpStatusCode.BadRequest);
    }
}
