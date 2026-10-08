using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Releaser.Domain.Deployments;
using Releaser.Domain.Policies;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Admin;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Audiences;
using Releaser.Server.Features.Audit;
using Releaser.Server.Features.ContextKeys;
using Releaser.Server.Features.Deployments;
using Releaser.Server.Features.Policies;
using Releaser.Server.Features.ReleaseNotes;
using Releaser.Server.Features.Releases;

namespace Releaser.Server.Tests.Infrastructure;

/// <summary>Typed wrapper over the admin API, as the dashboard would use it.</summary>
public sealed class AdminClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public AdminClient(HttpClient http)
    {
        Http = http;
        Http.DefaultRequestHeaders.Add("X-Releaser-Csrf", "1");
    }

    public HttpClient Http { get; }

    public async Task<AdminClient> LoginAsync(string email = ReleaserFactory.AdminEmail, string password = ReleaserFactory.AdminPassword)
    {
        var response = await Http.PostAsJsonAsync("/api/admin/v1/auth/login", new LoginRequest(email, password), Json);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return this;
    }

    public Task<ApplicationResponse> CreateApplicationAsync(string key = "sample-app", IReadOnlyList<PlatformTarget>? platforms = null) =>
        SendAsync<ApplicationResponse>(HttpMethod.Post, "/api/admin/v1/applications",
            new CreateApplicationRequest(key, "Sample App", null, "stable", "Stable", platforms ?? Enum.GetValues<PlatformTarget>()));

    public Task<ReleaseResponse> RegisterReleaseAsync(Guid appId, string version, IEnumerable<(PlatformTarget Platform, string Url)> manifests, params string[] channels) =>
        SendAsync<ReleaseResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/releases",
            new RegisterReleaseRequest(version, $"Version {version}", channels.Length == 0 ? ["stable"] : channels,
                [.. manifests.Select(m => new ManifestReference(m.Platform, m.Url))]));

    public Task<AudienceResponse> CreateAudienceAsync(Guid appId, string name, params AudienceRule[] includes) =>
        SendAsync<AudienceResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/audiences", new SaveAudienceRequest(name, null, includes, []));

    public Task<DeploymentResponse> CreateDeploymentAsync(Guid appId, string name, Guid releaseId, Guid audienceId, decimal percentage = 100m, string channel = "stable", int priority = 0) =>
        SendAsync<DeploymentResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/deployments",
            new CreateDeploymentRequest(name, releaseId, channel, audienceId, percentage, priority));

    public Task<DeploymentResponse> TransitionDeploymentAsync(Guid appId, Guid deploymentId, string verb) =>
        SendAsync<DeploymentResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/deployments/{deploymentId}/{verb}", null);

    public async Task<DeploymentResponse> DeployAsync(Guid appId, string name, Guid releaseId, Guid audienceId, decimal percentage = 100m, string channel = "stable", int priority = 0)
    {
        var deployment = await CreateDeploymentAsync(appId, name, releaseId, audienceId, percentage, channel, priority);
        return await TransitionDeploymentAsync(appId, deployment.Id, "activate");
    }

    public Task<DeploymentResponse> ChangeRolloutAsync(Guid appId, Guid deploymentId, decimal percentage, int priority = 0) =>
        SendAsync<DeploymentResponse>(HttpMethod.Put, $"/api/admin/v1/applications/{appId}/deployments/{deploymentId}/rollout", new ChangeRolloutRequest(percentage, priority));

    public Task<PolicyResponse> CreatePolicyAsync(Guid appId, string name, PolicyKind kind, Guid audienceId, Guid releaseId) =>
        SendAsync<PolicyResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/policies", new CreatePolicyRequest(name, kind, audienceId, releaseId, 0));

    public Task<ReleaseNoteResponse> SaveNotesAsync(Guid appId, Guid releaseId, SaveReleaseNoteRequest request) =>
        SendAsync<ReleaseNoteResponse>(HttpMethod.Put, $"/api/admin/v1/applications/{appId}/releases/{releaseId}/notes", request);

    public Task<ReleaseNoteResponse> PublishNotesAsync(Guid appId, Guid releaseId) =>
        SendAsync<ReleaseNoteResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/releases/{releaseId}/notes/publish", null);

    public Task<ContextKeyResponse> RegisterContextKeyAsync(Guid appId, string publicKeyPem) =>
        SendAsync<ContextKeyResponse>(HttpMethod.Post, $"/api/admin/v1/applications/{appId}/context-keys", new RegisterContextKeyRequest("Publisher backend", publicKeyPem));

    public Task<AuditPage> AuditAsync(Guid? appId = null, int limit = 200) =>
        SendAsync<AuditPage>(HttpMethod.Get, $"/api/admin/v1/audit?limit={limit}" + (appId is null ? "" : $"&appId={appId}"), null);

    public Task<CurrentUserResponse> CreateUserAsync(string email, string role) =>
        SendAsync<CurrentUserResponse>(HttpMethod.Post, "/api/admin/v1/users", new CreateUserRequest(email, email, ReleaserFactory.AdminPassword, role));

    public async Task<T> SendAsync<T>(HttpMethod method, string path, object? body)
    {
        var response = await RawAsync(method, path, body);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{method} {path} -> {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}", null, response.StatusCode);
        }
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public async Task<HttpResponseMessage> RawAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }
        return await Http.SendAsync(request);
    }
}
