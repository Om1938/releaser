using Releaser.Domain.Common;

namespace Releaser.Domain.Applications;

/// <summary>A logical release stream of an application (stable, beta, ...).</summary>
public sealed class Channel : IApplicationScoped
{
    private Channel() { } // EF Core

    public Guid Id { get; private set; }
    public AppId AppId { get; private set; }
    public ChannelKey Key { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public static Channel Create(AppId appId, ChannelKey key, string name, string? description) => new()
    {
        Id = Guid.CreateVersion7(),
        AppId = appId,
        Key = key,
        Name = name,
        Description = description,
    };

    public void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }
}
