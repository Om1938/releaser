using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.ContextKeys;
using Releaser.Server.Tests.Infrastructure;

namespace Releaser.Server.Tests;

/// <summary>Customer/user/group targeting only trusts publisher-signed tokens (ADR 0004).</summary>
public sealed class ContextTokenTests(PostgresContainer postgres) : PlatformTest(postgres)
{
    private readonly ContextTokens _publisher = new();

    private async Task<(AdminClient Admin, ApplicationResponse App, ContextKeyResponse Key)> ArrangeAsync()
    {
        var admin = await AdminAsync();
        var app = await admin.CreateApplicationAsync();
        var release = await admin.RegisterReleaseAsync(app.Id, "1.1.0", AllPlatforms(Manifests, "1.1.0"));
        var customerA = await admin.CreateAudienceAsync(app.Id, "Customer A", new CustomerRule(["customer-a"]));
        await admin.DeployAsync(app.Id, "Customer A", release.Id, customerA.Id);
        var key = await admin.RegisterContextKeyAsync(app.Id, _publisher.PublicKeyPem);
        return (admin, app, key);
    }

    private Task<FeedAnswer> CheckAsync(string? token, string installation = "installation-1") =>
        Node.CreateFeedClient().CheckAsync(installation, "1.0.0", token: token);

    [Fact]
    public async Task a_valid_token_unlocks_customer_targeting()
    {
        await ArrangeAsync();
        (await CheckAsync(_publisher.Sign("sample-app", customer: "customer-a"))).IsOffer("1.1.0").ShouldBeTrue();
    }

    [Fact]
    public async Task without_a_token_customer_targeting_does_not_apply()
    {
        await ArrangeAsync();
        (await CheckAsync(null)).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task tokens_signed_by_an_unregistered_key_are_ignored()
    {
        await ArrangeAsync();
        using var attacker = new ContextTokens();
        (await CheckAsync(attacker.Sign("sample-app", customer: "customer-a"))).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task tokens_for_another_application_are_ignored()
    {
        await ArrangeAsync();
        (await CheckAsync(_publisher.Sign("other-app", customer: "customer-a"))).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task expired_and_overlong_tokens_are_ignored()
    {
        await ArrangeAsync();
        (await CheckAsync(_publisher.Sign("sample-app", customer: "customer-a", issuedAt: DateTime.UtcNow.AddHours(-2), lifetime: TimeSpan.FromHours(1)))).FileUrls.ShouldBeEmpty();
        (await CheckAsync(_publisher.Sign("sample-app", customer: "customer-a", lifetime: TimeSpan.FromDays(30)))).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task tokens_bound_to_another_installation_are_ignored()
    {
        await ArrangeAsync();
        var token = _publisher.Sign("sample-app", customer: "customer-a", installation: "installation-other");
        (await CheckAsync(token)).FileUrls.ShouldBeEmpty();
        (await CheckAsync(token, "installation-other")).IsOffer("1.1.0").ShouldBeTrue();
    }

    [Fact]
    public async Task revoking_the_key_stops_trusting_its_tokens()
    {
        var (admin, app, key) = await ArrangeAsync();
        await admin.SendAsync<ContextKeyResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/context-keys/{key.Id}/revoke", null);
        (await CheckAsync(_publisher.Sign("sample-app", customer: "customer-a"))).FileUrls.ShouldBeEmpty();
    }

    [Fact]
    public async Task private_keys_are_refused_at_registration()
    {
        var (admin, app, _) = await ArrangeAsync();
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var response = await admin.RawAsync(HttpMethod.Post, $"/api/admin/v1/applications/{app.Id}/context-keys",
            new RegisterContextKeyRequest("leaked", ecdsa.ExportPkcs8PrivateKeyPem()));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }
}
