using Releaser.Domain.Common;

namespace Releaser.Domain.Targeting;

/// <summary>Immutable matching definition of an audience: any include rule matches and no exclude rule matches.</summary>
public sealed record AudienceDefinition(AudienceId Id, IReadOnlyList<AudienceRule> Includes, IReadOnlyList<AudienceRule> Excludes)
{
    /// <summary>Returns the specificity of the most specific matching include rule, or null when the audience does not match.</summary>
    public AudienceSpecificity? Match(TargetContext context)
    {
        if (Excludes.Any(rule => rule.Matches(context)))
        {
            return null;
        }
        var matching = Includes.Where(rule => rule.Matches(context)).ToList();
        return matching.Count == 0 ? null : matching.Max(rule => rule.Specificity);
    }
}
