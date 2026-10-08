namespace Releaser.Domain.Deployments;

public enum DeploymentState
{
    Draft = 1,
    Active = 2,
    Paused = 3,
    Completed = 4,
    Cancelled = 5,
}
