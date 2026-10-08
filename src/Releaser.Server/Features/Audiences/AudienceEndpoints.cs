using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Domain.Targeting;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Audiences;

public sealed record AudienceResponse(Guid Id, string Name, string? Description, IReadOnlyList<AudienceRule> Includes, IReadOnlyList<AudienceRule> Excludes)
{
    public static AudienceResponse From(Audience a) => new(a.Id.Value, a.Name, a.Description, a.Includes, a.Excludes);
}

public sealed record SaveAudienceRequest(string Name, string? Description, IReadOnlyList<AudienceRule> Includes, IReadOnlyList<AudienceRule> Excludes);

internal sealed class SaveAudienceValidator : AbstractValidator<SaveAudienceRequest>
{
    private const int MaxValues = 5_000;

    public SaveAudienceValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(2000);
        RuleFor(r => r.Includes).NotEmpty().WithMessage("Add at least one include rule.");
        RuleFor(r => r.Excludes).NotNull();
        RuleForEach(r => r.Includes).Must(BeValid).WithMessage(Explanation);
        RuleForEach(r => r.Excludes).Must(BeValid).WithMessage(Explanation);
    }

    private const string Explanation =
        "Each rule needs 1-5000 non-empty values of at most 128 characters; version bounds must be semantic versions.";

    private static bool BeValid(AudienceRule? rule) => rule switch
    {
        EveryoneRule => true,
        InstallationRule r => ValidValues(r.InstallationIds),
        UserRule r => ValidValues(r.UserIds),
        GroupRule r => ValidValues(r.Groups),
        CustomerRule r => ValidValues(r.CustomerIds),
        AttributeRule r => !string.IsNullOrWhiteSpace(r.Name) && r.Name.Length <= 64 && ValidValues(r.Values),
        PlatformRule r => r.Platforms is { Count: > 0 } && r.Platforms.All(Enum.IsDefined),
        CurrentVersionRule r => (r.Minimum ?? r.MaximumExclusive) is not null && IsVersionOrNull(r.Minimum) && IsVersionOrNull(r.MaximumExclusive),
        _ => false,
    };

    private static bool ValidValues(IReadOnlyList<string>? values) =>
        values is { Count: > 0 and <= MaxValues } && values.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 128);

    private static bool IsVersionOrNull(string? version) => version is null || SemanticVersion.TryParse(version, out _);
}

internal static class AudienceEndpoints
{
    public static RouteGroupBuilder MapAudiences(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/audiences").WithTags("Audiences");
        group.MapGet("/", ListAsync).WithName("ListAudiences");
        group.MapPost("/", CreateAsync).WithName("CreateAudience").Validate<SaveAudienceRequest>().RequiresReleaseManager();
        group.MapPut("/{audienceId:guid}", UpdateAsync).WithName("UpdateAudience").Validate<SaveAudienceRequest>().RequiresReleaseManager();
        group.MapDelete("/{audienceId:guid}", DeleteAsync).WithName("DeleteAudience").RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<AudienceResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var audiences = await db.Audiences.AsNoTracking().Where(a => a.AppId == new AppId(appId)).OrderBy(a => a.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok(audiences.Select(AudienceResponse.From).ToList());
    }

    private static async Task<Results<Created<AudienceResponse>, NotFound>> CreateAsync(
        Guid appId, SaveAudienceRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == new AppId(appId), cancellationToken))
        {
            return TypedResults.NotFound();
        }
        var audience = Audience.Create(new AppId(appId), request.Name, request.Description, request.Includes, request.Excludes);
        db.Audiences.Add(audience);
        audit.Record(new AuditRecord("audience.created", "audience", audience.Id.ToString(), appId, Describe(audience)));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/audiences/{audience.Id}", AudienceResponse.From(audience));
    }

    private static async Task<Results<Ok<AudienceResponse>, NotFound>> UpdateAsync(
        Guid appId, Guid audienceId, SaveAudienceRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var audience = await db.Audiences.SingleOrDefaultAsync(a => a.AppId == new AppId(appId) && a.Id == new AudienceId(audienceId), cancellationToken);
        if (audience is null)
        {
            return TypedResults.NotFound();
        }
        audience.Update(request.Name, request.Description, request.Includes, request.Excludes);
        audit.Record(new AuditRecord("audience.updated", "audience", audience.Id.ToString(), appId, Describe(audience)));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(AudienceResponse.From(audience));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid appId, Guid audienceId, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var id = new AudienceId(audienceId);
        var audience = await db.Audiences.SingleOrDefaultAsync(a => a.AppId == new AppId(appId) && a.Id == id, cancellationToken);
        if (audience is null)
        {
            return TypedResults.NotFound();
        }
        if (await db.Deployments.AnyAsync(d => d.AudienceId == id, cancellationToken) || await db.Policies.AnyAsync(p => p.AudienceId == id, cancellationToken))
        {
            throw new DomainRuleException("audience.in_use", "The audience is used by deployments or policies and cannot be deleted.");
        }
        db.Audiences.Remove(audience);
        audit.Record(new AuditRecord("audience.deleted", "audience", audience.Id.ToString(), appId, $"name={audience.Name}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Audit summary without listing member identifiers (privacy).</summary>
    private static string Describe(Audience audience) =>
        $"name={audience.Name}; includes={string.Join(',', audience.Includes.Select(r => r.GetType().Name))}; excludes={string.Join(',', audience.Excludes.Select(r => r.GetType().Name))}";
}
