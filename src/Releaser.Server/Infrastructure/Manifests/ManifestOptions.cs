using System.ComponentModel.DataAnnotations;

namespace Releaser.Server.Infrastructure.Manifests;

public sealed class ManifestOptions
{
    public const string Section = "Manifests";

    /// <summary>Allow http:// manifest URLs. Development and the bundled sample only.</summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>Allow manifest hosts that resolve to loopback/private/link-local addresses. Development and the bundled sample only.</summary>
    public bool AllowPrivateNetworks { get; set; }

    [Range(1, 60)]
    public int TimeoutSeconds { get; set; } = 10;
}
