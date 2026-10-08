using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Policies;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Policies;

public sealed record PolicyResponse(Guid Id, string Name, PolicyKind Kind, Guid AudienceId, Guid ReleaseId, string ReleaseVersion, int Priority, DateTimeOffset CreatedAt);

public sealed record CreatePolicyRequest(string Name, PolicyKind Kind, Guid AudienceId, Guid ReleaseId, int Priority);

internal sealed class CreatePolicyValidator : AbstractValidator<CreatePolicyRequest>
{
    public CreatePolicyValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Kind).IsInEnum();
        RuleFor(r => r.AudienceId).NotEmpty();
        RuleFor(r => r.ReleaseId).NotEmpty();
        RuleFor(r => r.Priority).InclusiveBetween(-1000, 1000);
    }
}

internal static class PolicyEndpoints
{
    public static RouteGroupBuilder MapPolicies(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/policies").WithTags("Policies");
        group.MapGet("/", ListAsync).WithName("ListPolicies");
        group.MapPost("/", CreateAsync).WithName("CreatePolicy").Validate<CreatePolicyRequest>().RequiresReleaseManager();
        group.MapDelete("/{policyId:guid}", DeleteAsync).WithName("DeletePolicy").RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<PolicyResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var rows = await (
            from policy in db.Policies.AsNoTracking().Where(p => p.AppId == new AppId(appId))
            join release in db.Releases.AsNoTracking() on policy.ReleaseId equals release.Id
            select new { policy, release.Version })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows.OrderBy(r => r.policy.Kind).ThenBy(r => r.policy.Name).Select(r => ToResponse(r.policy, r.Version)).ToList());
    }

    private static async Task<Results<Created<PolicyResponse>, NotFound>> CreateAsync(
        Guid appId, CreatePolicyRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        var release = await db.Releases.AsNoTracking().SingleOrDefaultAsync(r => r.AppId == id && r.Id == new ReleaseId(request.ReleaseId), cancellationToken);
        var audienceExists = await db.Audiences.AnyAsync(a => a.AppId == id && a.Id == new AudienceId(request.AudienceId), cancellationToken);
        if (release is null || !audienceExists)
        {
            return TypedResults.NotFound();
        }
        var now = clock.GetUtcNow();
        var policy = request.Kind == PolicyKind.Pin
            ? Policy.Pin(id, request.Name, new AudienceId(request.AudienceId), release.Id, request.Priority, now)
            : Policy.Exclude(id, request.Name, new AudienceId(request.AudienceId), release.Id, now);
        db.Policies.Add(policy);
        audit.Record(new AuditRecord(request.Kind == PolicyKind.Pin ? "policy.pin_created" : "policy.exclusion_created", "policy", policy.Id.ToString(), appId,
            $"name={policy.Name}; release={release.Version}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/policies/{policy.Id}", ToResponse(policy, release.Version));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(Guid appId, Guid policyId, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var policy = await db.Policies.SingleOrDefaultAsync(p => p.AppId == new AppId(appId) && p.Id == new PolicyId(policyId), cancellationToken);
        if (policy is null)
        {
            return TypedResults.NotFound();
        }
        db.Policies.Remove(policy);
        audit.Record(new AuditRecord("policy.deleted", "policy", policy.Id.ToString(), appId, $"name={policy.Name}; kind={policy.Kind}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static PolicyResponse ToResponse(Policy p, SemanticVersion version) =>
        new(p.Id.Value, p.Name, p.Kind, p.AudienceId.Value, p.ReleaseId.Value, version.Value, p.Priority, p.CreatedAt);
}
