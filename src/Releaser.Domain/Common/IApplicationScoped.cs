namespace Releaser.Domain.Common;

/// <summary>Entity whose changes alter update resolution for its application (bumps the configuration version, ADR 0008).</summary>
public interface IApplicationScoped
{
    AppId AppId { get; }
}
