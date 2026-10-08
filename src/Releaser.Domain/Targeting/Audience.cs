using Releaser.Domain.Common;

namespace Releaser.Domain.Targeting;

/// <summary>A named set of release targets (customers, users, groups, installations, attributes).</summary>
public sealed class Audience : IApplicationScoped
{
    private Audience() { } // EF Core

    public AudienceId Id { get; private set; }
    public AppId AppId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public IReadOnlyList<AudienceRule> Includes { get; private set; } = [];
    public IReadOnlyList<AudienceRule> Excludes { get; private set; } = [];

    public static Audience Create(AppId appId, string name, string? description, IReadOnlyList<AudienceRule> includes, IReadOnlyList<AudienceRule> excludes)
    {
        var audience = new Audience { Id = AudienceId.New(), AppId = appId };
        audience.Update(name, description, includes, excludes);
        return audience;
    }

    public void Update(string name, string? description, IReadOnlyList<AudienceRule> includes, IReadOnlyList<AudienceRule> excludes)
    {
        if (includes.Count == 0)
        {
            throw new DomainRuleException("audience.no_includes", "An audience needs at least one include rule.");
        }
        Name = name;
        Description = description;
        Includes = [.. includes];
        Excludes = [.. excludes];
    }

    public AudienceDefinition ToDefinition() => new(Id, Includes, Excludes);
}
