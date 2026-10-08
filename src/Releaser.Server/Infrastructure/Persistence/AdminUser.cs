using Microsoft.AspNetCore.Identity;

namespace Releaser.Server.Infrastructure.Persistence;

/// <summary>A publisher administrator (local ASP.NET Core Identity account).</summary>
public sealed class AdminUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
