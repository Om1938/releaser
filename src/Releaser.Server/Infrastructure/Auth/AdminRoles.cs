namespace Releaser.Server.Infrastructure.Auth;

/// <summary>Administrator roles and the authorization policies built from them (ADR 0009).</summary>
public static class AdminRoles
{
    public const string Admin = "Admin";
    public const string ReleaseManager = "ReleaseManager";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, ReleaseManager, Viewer];

    /// <summary>Read access to the admin API.</summary>
    public const string CanView = "CanView";

    /// <summary>Manage applications, releases, audiences, deployments, policies, notes and keys.</summary>
    public const string CanManageReleases = "CanManageReleases";

    /// <summary>Permanently delete releases (issue #12). Admins only: it cannot be undone.</summary>
    public const string CanObliterateReleases = "CanObliterateReleases";

    /// <summary>Manage administrator accounts.</summary>
    public const string CanManageUsers = "CanManageUsers";
}
