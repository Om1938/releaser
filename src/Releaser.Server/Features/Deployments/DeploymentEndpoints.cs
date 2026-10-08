using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Deployments;
using Releaser.Domain.Rollouts;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Deployments;

public sealed record DeploymentResponse(
    Guid Id, string Name, Guid ReleaseId, string ReleaseVersion, string Channel, Guid AudienceId, string AudienceName,
    decimal Percentage, int Priority, DeploymentState State, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateDeploymentRequest(string Name, Guid ReleaseId, string Channel, Guid AudienceId, decimal Percentage, int Priority);

public sealed record ChangeRolloutRequest(decimal Percentage, int Priority);

internal sealed class CreateDeploymentValidator : AbstractValidator<CreateDeploymentRequest>
{
    public CreateDeploymentValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.ReleaseId).NotEmpty();
        RuleFor(r => r.AudienceId).NotEmpty();
        RuleFor(r => r.Channel).MustBeChannelKey();
        RuleFor(r => r.Percentage).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(r => r.Priority).InclusiveBetween(-1000, 1000);
    }
}

internal sealed class ChangeRolloutValidator : AbstractValidator<ChangeRolloutRequest>
{
    public ChangeRolloutValidator()
    {
        RuleFor(r => r.Percentage).InclusiveBetween(0, 100).PrecisionScale(5, 2, true);
        RuleFor(r => r.Priority).InclusiveBetween(-1000, 1000);
    }
}

internal static class DeploymentEndpoints
{
    public static RouteGroupBuilder MapDeployments(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/deployments").WithTags("Deployments");
        group.MapGet("/", ListAsync).WithName("ListDeployments");
        group.MapPost("/", CreateAsync).WithName("CreateDeployment").Validate<CreateDeploymentRequest>().RequiresReleaseManager();
        group.MapPut("/{deploymentId:guid}/rollout", ChangeRolloutAsync).WithName("ChangeDeploymentRollout").Validate<ChangeRolloutRequest>().RequiresReleaseManager();
        MapTransition(group, "activate", "ActivateDeployment", (d, now) => d.Activate(now));
        MapTransition(group, "pause", "PauseDeployment", (d, now) => d.Pause(now));
        MapTransition(group, "resume", "ResumeDeployment", (d, now) => d.Resume(now));
        MapTransition(group, "complete", "CompleteDeployment", (d, now) => d.Complete(now));
        MapTransition(group, "cancel", "CancelDeployment", (d, now) => d.Cancel(now));
        return admin;
    }

    private static void MapTransition(RouteGroupBuilder group, string verb, string name, Action<Deployment, DateTimeOffset> transition) =>
        group.MapPost($"/{{deploymentId:guid}}/{verb}",
                async Task<Results<Ok<DeploymentResponse>, NotFound>> (Guid appId, Guid deploymentId, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken ct) =>
                {
                    var deployment = await FindAsync(appId, deploymentId, db, ct);
                    if (deployment is null)
                    {
                        return TypedResults.NotFound();
                    }
                    var previous = deployment.State;
                    transition(deployment, clock.GetUtcNow());
                    audit.Record(new AuditRecord($"deployment.{verb}", "deployment", deployment.Id.ToString(), appId, $"name={deployment.Name}; {previous} -> {deployment.State}"));
                    await db.SaveChangesAsync(ct);
                    return TypedResults.Ok(await ToResponseAsync(deployment, db, ct));
                })
            .WithName(name)
            .RequiresReleaseManager();

    private static async Task<Ok<List<DeploymentResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        var rows = await (
            from deployment in db.Deployments.AsNoTracking().Where(d => d.AppId == id)
            join release in db.Releases.AsNoTracking() on deployment.ReleaseId equals release.Id
            join audience in db.Audiences.AsNoTracking() on deployment.AudienceId equals audience.Id
            select new { deployment, release.Version, audience.Name })
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(rows
            .OrderBy(r => r.deployment.State)
            .ThenByDescending(r => r.deployment.UpdatedAt)
            .Select(r => ToResponse(r.deployment, r.Version, r.Name))
            .ToList());
    }

    private static async Task<Results<Created<DeploymentResponse>, NotFound>> CreateAsync(
        Guid appId, CreateDeploymentRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        var id = new AppId(appId);
        var channel = ChannelKey.From(request.Channel);
        var release = await db.Releases.SingleOrDefaultAsync(r => r.AppId == id && r.Id == new ReleaseId(request.ReleaseId), cancellationToken);
        var audienceExists = await db.Audiences.AnyAsync(a => a.AppId == id && a.Id == new AudienceId(request.AudienceId), cancellationToken);
        if (release is null || !audienceExists)
        {
            return TypedResults.NotFound();
        }
        if (!release.IsOfferable || !release.Channels.Contains(channel))
        {
            throw new DomainRuleException("deployment.release_not_offerable",
                $"Release {release.Version} must be available and assigned to channel '{channel}' before it can be deployed there.");
        }
        var deployment = Deployment.Create(new DeploymentPlan(id, request.Name, release.Id, channel, new AudienceId(request.AudienceId),
            RolloutPercentage.From(request.Percentage), request.Priority), clock.GetUtcNow());
        db.Deployments.Add(deployment);
        audit.Record(new AuditRecord("deployment.created", "deployment", deployment.Id.ToString(), appId,
            $"name={deployment.Name}; release={release.Version}; channel={channel}; percentage={deployment.Percentage}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/deployments/{deployment.Id}", await ToResponseAsync(deployment, db, cancellationToken));
    }

    private static async Task<Results<Ok<DeploymentResponse>, NotFound>> ChangeRolloutAsync(
        Guid appId, Guid deploymentId, ChangeRolloutRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        var deployment = await FindAsync(appId, deploymentId, db, cancellationToken);
        if (deployment is null)
        {
            return TypedResults.NotFound();
        }
        var previous = deployment.Percentage;
        deployment.ChangePercentage(RolloutPercentage.From(request.Percentage), clock.GetUtcNow());
        deployment.ChangePriority(request.Priority, clock.GetUtcNow());
        audit.Record(new AuditRecord("deployment.rollout_changed", "deployment", deployment.Id.ToString(), appId,
            $"name={deployment.Name}; percentage {previous} -> {deployment.Percentage}; priority={deployment.Priority}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await ToResponseAsync(deployment, db, cancellationToken));
    }

    private static Task<Deployment?> FindAsync(Guid appId, Guid deploymentId, ReleaserDbContext db, CancellationToken cancellationToken) =>
        db.Deployments.SingleOrDefaultAsync(d => d.AppId == new AppId(appId) && d.Id == new DeploymentId(deploymentId), cancellationToken);

    private static async Task<DeploymentResponse> ToResponseAsync(Deployment deployment, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var version = await db.Releases.Where(r => r.Id == deployment.ReleaseId).Select(r => r.Version).SingleAsync(cancellationToken);
        var audience = await db.Audiences.Where(a => a.Id == deployment.AudienceId).Select(a => a.Name).SingleAsync(cancellationToken);
        return ToResponse(deployment, version, audience);
    }

    private static DeploymentResponse ToResponse(Deployment d, SemanticVersion version, string audienceName) =>
        new(d.Id.Value, d.Name, d.ReleaseId.Value, version.Value, d.Channel.Value, d.AudienceId.Value, audienceName,
            d.Percentage.Value, d.Priority, d.State, d.CreatedAt, d.UpdatedAt);
}
