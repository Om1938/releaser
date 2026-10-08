namespace Releaser.Domain.Common;

public readonly record struct AppId(Guid Value)
{
    public static AppId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct ReleaseId(Guid Value)
{
    public static ReleaseId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct DeploymentId(Guid Value)
{
    public static DeploymentId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct AudienceId(Guid Value)
{
    public static AudienceId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}

public readonly record struct PolicyId(Guid Value)
{
    public static PolicyId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
