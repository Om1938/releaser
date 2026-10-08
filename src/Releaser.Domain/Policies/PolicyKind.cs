namespace Releaser.Domain.Policies;

public enum PolicyKind
{
    /// <summary>Holds an audience on one specific release, overriding deployments.</summary>
    Pin = 1,

    /// <summary>Never offers a specific release to an audience.</summary>
    Exclusion = 2,
}
