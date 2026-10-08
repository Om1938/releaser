using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Applications;
using Releaser.Domain.Common;
using Releaser.Server.Features.Applications;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Channels;

public sealed record ChannelResponse(Guid Id, string Key, string Name, string? Description)
{
    public static ChannelResponse From(Channel c) => new(c.Id, c.Key.Value, c.Name, c.Description);
}

public sealed record CreateChannelRequest(string Key, string Name, string? Description);

public sealed record UpdateChannelRequest(string Name, string? Description);

internal sealed class CreateChannelValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelValidator()
    {
        RuleFor(r => r.Key).MustBeChannelKey();
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(2000);
    }
}

internal sealed class UpdateChannelValidator : AbstractValidator<UpdateChannelRequest>
{
    public UpdateChannelValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Description).MaximumLength(2000);
    }
}

internal static class ChannelEndpoints
{
    public static RouteGroupBuilder MapChannels(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/applications/{appId:guid}/channels").WithTags("Channels");
        group.MapGet("/", ListAsync).WithName("ListChannels");
        group.MapPost("/", CreateAsync).WithName("CreateChannel").Validate<CreateChannelRequest>().RequiresReleaseManager();
        group.MapPut("/{channelId:guid}", UpdateAsync).WithName("UpdateChannel").Validate<UpdateChannelRequest>().RequiresReleaseManager();
        return admin;
    }

    private static async Task<Ok<List<ChannelResponse>>> ListAsync(Guid appId, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var channels = await db.Channels.AsNoTracking().Where(c => c.AppId == new AppId(appId)).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return TypedResults.Ok(channels.Select(ChannelResponse.From).ToList());
    }

    private static async Task<Results<Created<ChannelResponse>, NotFound>> CreateAsync(
        Guid appId, CreateChannelRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        if (!await db.Applications.AnyAsync(a => a.Id == new AppId(appId), cancellationToken))
        {
            return TypedResults.NotFound();
        }
        var channel = Channel.Create(new AppId(appId), ChannelKey.From(request.Key), request.Name, request.Description);
        db.Channels.Add(channel);
        audit.Record(new AuditRecord("channel.created", "channel", channel.Id.ToString(), appId, $"key={channel.Key}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/applications/{appId}/channels/{channel.Id}", ChannelResponse.From(channel));
    }

    private static async Task<Results<Ok<ChannelResponse>, NotFound>> UpdateAsync(
        Guid appId, Guid channelId, UpdateChannelRequest request, ReleaserDbContext db, IAuditLog audit, CancellationToken cancellationToken)
    {
        var channel = await db.Channels.SingleOrDefaultAsync(c => c.AppId == new AppId(appId) && c.Id == channelId, cancellationToken);
        if (channel is null)
        {
            return TypedResults.NotFound();
        }
        channel.Rename(request.Name, request.Description);
        audit.Record(new AuditRecord("channel.updated", "channel", channel.Id.ToString(), appId));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ChannelResponse.From(channel));
    }
}
