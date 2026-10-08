using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Resolution;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Applications;
using Releaser.Server.Infrastructure.Caching;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Decisions;

/// <summary>Hypothetical installation context to explain. Identity fields are treated as already verified.</summary>
public sealed record ExplainDecisionRequest(
    string? Channel, PlatformTarget Platform, string CurrentVersion, string? InstallationId,
    string? CustomerId, string? UserId, IReadOnlyList<string>? Groups, IReadOnlyDictionary<string, string>? Attributes);

public sealed record ExplainDecisionResponse(
    bool UpdateOffered, DecisionReason Reason, string? OfferedVersion, string? ReleaseVersion, Guid? DeploymentId, string? DeploymentName,
    string? PinName, string Channel, long ConfigVersion, IReadOnlyList<string> Trace);

internal sealed class ExplainDecisionValidator : AbstractValidator<ExplainDecisionRequest>
{
    public ExplainDecisionValidator()
    {
        RuleFor(r => r.Channel!).MustBeChannelKey().When(r => r.Channel is not null);
        RuleFor(r => r.Platform).IsInEnum();
        RuleFor(r => r.CurrentVersion).Must(v => SemanticVersion.TryParse(v, out _)).WithMessage("Use a semantic version such as 1.0.0.");
        RuleFor(r => r.InstallationId).Must(id => id is null || Domain.Targeting.InstallationId.TryFrom(id, out _))
            .WithMessage("Installation ids are 8-128 characters of letters, digits, '.', '_' or '-'.");
    }
}

internal static class ExplainDecisionEndpoint
{
    public static RouteGroupBuilder MapDecisionExplainer(this RouteGroupBuilder admin)
    {
        admin.MapPost("/applications/{appId:guid}/decisions/explain", ExplainAsync)
            .WithName("ExplainDecision")
            .WithTags("Decisions")
            .WithSummary("Explain the update decision for a hypothetical installation")
            .Validate<ExplainDecisionRequest>();
        return admin;
    }

    private static async Task<Results<Ok<ExplainDecisionResponse>, NotFound>> ExplainAsync(
        Guid appId, ExplainDecisionRequest request, ReleaserDbContext db, FeedSnapshotCache snapshots, CancellationToken cancellationToken)
    {
        var key = await db.Applications.Where(a => a.Id == new AppId(appId)).Select(a => a.Key).SingleOrDefaultAsync(cancellationToken);
        var snapshot = key is null ? null : await snapshots.GetAsync(key, cancellationToken);
        if (snapshot is null)
        {
            return TypedResults.NotFound();
        }
        var channel = request.Channel is null ? snapshot.Resolution.DefaultChannel : ChannelKey.From(request.Channel);
        var identity = new VerifiedIdentity(request.UserId, request.CustomerId, new HashSet<string>(request.Groups ?? []),
            new Dictionary<string, string>(request.Attributes ?? new Dictionary<string, string>()));
        var context = new TargetContext(channel, request.Platform, SemanticVersion.Parse(request.CurrentVersion),
            request.InstallationId is null ? null : InstallationId.From(request.InstallationId), identity);
        var decision = UpdateResolver.Resolve(snapshot.Resolution, context);
        return TypedResults.Ok(new ExplainDecisionResponse(decision.IsUpdateOffered, decision.Reason, decision.OfferedVersion?.Value,
            decision.Release?.Version.Value, decision.Deployment?.Id.Value, decision.Deployment?.Name, decision.AppliedPin?.Name,
            channel.Value, snapshot.Resolution.ConfigVersion, decision.Trace));
    }
}
