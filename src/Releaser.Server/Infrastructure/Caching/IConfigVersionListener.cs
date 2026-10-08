using Releaser.Domain.Common;

namespace Releaser.Server.Infrastructure.Caching;

/// <summary>Notified after a commit that changed an application's resolution inputs, so this node can drop its version stamp immediately.</summary>
public interface IConfigVersionListener
{
    void ConfigVersionChanged(AppId application);
}
