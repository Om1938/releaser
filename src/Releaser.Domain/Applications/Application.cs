using Releaser.Domain.Common;

namespace Releaser.Domain.Applications;

/// <summary>An independently managed application with its own release history.</summary>
public sealed class Application
{
    private Application() { } // EF Core

    public AppId Id { get; private set; }
    public ApplicationKey Key { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ChannelKey DefaultChannel { get; private set; } = null!;

    /// <summary>Monotonic version of everything that affects resolution; incremented atomically by persistence (ADR 0008).</summary>
    public long ConfigVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Application Create(ApplicationKey key, string name, string? description, ChannelKey defaultChannel, DateTimeOffset now) => new()
    {
        Id = AppId.New(),
        Key = key,
        Name = name,
        Description = description,
        DefaultChannel = defaultChannel,
        ConfigVersion = 1,
        CreatedAt = now,
    };

    public void Rename(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    public void ChangeDefaultChannel(ChannelKey channel) => DefaultChannel = channel;
}
