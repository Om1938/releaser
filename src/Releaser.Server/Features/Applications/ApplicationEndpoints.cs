using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Applications;
using Releaser.Domain.Common;
using Releaser.Domain.Targeting;
using Releaser.Electron;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Applications;

public sealed record ApplicationResponse(
    Guid Id, string Key, string Name, string? Description, string DefaultChannel, IReadOnlyList<PlatformTarget> SupportedPlatforms,
    long ConfigVersion, DateTimeOffset CreatedAt)
{
    public static ApplicationResponse From(Application a) =>
        new(a.Id.Value, a.Key.Value, a.Name, a.Description, a.DefaultChannel.Value, a.SupportedPlatforms, a.ConfigVersion, a.CreatedAt);
}

public sealed record CreateApplicationRequest(
    string Key, string Name, string? Description, string DefaultChannelKey, string DefaultChannelName, IReadOnlyList<PlatformTarget> SupportedPlatforms);

public sealed record UpdateApplicationRequest(string Name, string? Description, string DefaultChannelKey, IReadOnlyList<PlatformTarget> SupportedPlatforms);

internal sealed class CreateApplicationValidator : AbstractValidator<CreateApplicationRequest>
{
    public CreateApplicationValidator()
    {
        RuleFor(r => r.Key).MustBeSlug();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(2000);
        RuleFor(r => r.DefaultChannelKey).MustBeChannelKey();
        RuleFor(r => r.DefaultChannelName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.SupportedPlatforms).MustListPlatforms();
    }
}

internal sealed class UpdateApplicationValidator : AbstractValidator<UpdateApplicationRequest>
{
    public UpdateApplicationValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(2000);
        RuleFor(r => r.DefaultChannelKey).MustBeChannelKey();
        RuleFor(r => r.SupportedPlatforms).MustListPlatforms();
    }
}

internal static class ApplicationEndpoints
{
    public static RouteGroupBuilder MapApplications(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications").WithTags("Applications");
        group.MapGet("/", ListAsync).WithName("ListApplications");
        group.MapGet("/{appId:guid}", GetAsync).WithName("GetApplication");
        group.MapPost("/", CreateAsync).WithName("CreateApplication").Validate<CreateApplicationRequest>().RequiresReleaseManager();
        group.MapPut("/{appId:guid}", UpdateAsync).WithName("UpdateApplication").Validate<UpdateApplicationRequest>().RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<ApplicationResponse>>> ListAsync(ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var applications = await db.Applications.AsNoTracking().OrderBy(a => a.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok(applications.Select(ApplicationResponse.From).ToList());
    }

    private static async Task<Results<Ok<ApplicationResponse>, NotFound>> GetAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var application = await db.Applications.AsNoTracking().SingleOrDefaultAsync(a => a.Id == new AppId(appId), cancellationToken);
        return application is null ? TypedResults.NotFound() : TypedResults.Ok(ApplicationResponse.From(application));
    }

    private static async Task<Created<ApplicationResponse>> CreateAsync(
        CreateApplicationRequest request, ReleaserDbContext db, IAuditLog audit, TimeProvider clock, CancellationToken cancellationToken)
    {
        var defaultChannel = ChannelKey.From(request.DefaultChannelKey);
        var application = Application.Create(ApplicationKey.From(request.Key), request.Name, request.Description, defaultChannel, request.SupportedPlatforms, clock.GetUtcNow());
        db.Applications.Add(application);
        db.Channels.Add(Channel.Create(application.Id, defaultChannel, request.DefaultChannelName, null));
        audit.Record(new AuditRecord("application.created", "application", application.Id.ToString(), application.Id.Value,
            $"key={application.Key}; platforms={string.Join(',', application.SupportedPlatforms)}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{application.Id}", ApplicationResponse.From(application));
    }

    private static async Task<Results<Ok<ApplicationResponse>, NotFound>> UpdateAsync(
        Guid appId, UpdateApplicationRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == new AppId(appId), cancellationToken);
        if (application is null)
        {
            return TypedResults.NotFound();
        }
        var defaultChannel = ChannelKey.From(request.DefaultChannelKey);
        if (!await db.Channels.AnyAsync(c => c.AppId == application.Id && c.Key == defaultChannel, cancellationToken))
        {
            throw new DomainRuleException("application.unknown_channel", $"Channel '{defaultChannel}' does not exist.");
        }
        application.Rename(request.Name, request.Description);
        application.ChangeDefaultChannel(defaultChannel);
        application.ChangeSupportedPlatforms(request.SupportedPlatforms);
        audit.Record(new AuditRecord("application.updated", "application", application.Id.ToString(), application.Id.Value,
            $"defaultChannel={defaultChannel}; platforms={string.Join(',', application.SupportedPlatforms)}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ApplicationResponse.From(application));
    }
}

/// <summary>Validation rules shared by slices that accept keys.</summary>
internal static class KeyValidationRules
{
    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Matches("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$")
            .WithMessage("Use 1-64 lowercase letters, digits or dashes, not starting or ending with a dash.");

    public static IRuleBuilderOptions<T, IReadOnlyList<PlatformTarget>> MustListPlatforms<T>(this IRuleBuilder<T, IReadOnlyList<PlatformTarget>> rule) =>
        rule.NotEmpty().WithMessage("Choose at least one supported platform.")
            .Must(platforms => platforms.All(Enum.IsDefined)).WithMessage("Unknown platform.");

    public static IRuleBuilderOptions<T, string> MustBeChannelKey<T>(this IRuleBuilder<T, string> rule) =>
        rule.MustBeSlug().Must(FeedFileName.IsUnambiguousChannelKey)
            .WithMessage("Channel keys cannot be 'latest' or end with -mac/-linux/-linux-arm64/-linux-armv7l (reserved by electron-updater file names).");
}
