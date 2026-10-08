using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Releaser.Domain.Common;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Auth;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Admin;

public sealed record CreateUserRequest(string Email, string DisplayName, string Password, string Role);

public sealed record UpdateUserRequest(string DisplayName, string Role);

internal sealed class CreateUserValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(r => r.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Password).NotEmpty().MinimumLength(12).MaximumLength(256);
        RuleFor(r => r.Role).Must(AdminRoles.All.Contains).WithMessage($"Role must be one of: {string.Join(", ", AdminRoles.All)}.");
    }
}

internal sealed class UpdateUserValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(r => r.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(r => r.Role).Must(AdminRoles.All.Contains).WithMessage($"Role must be one of: {string.Join(", ", AdminRoles.All)}.");
    }
}

/// <summary>Administrator account management (Admin role only).</summary>
internal static class UserEndpoints
{
    public static RouteGroupBuilder MapUsers(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/users").WithTags("Users").RequireAuthorization(AdminRoles.CanManageUsers);
        group.MapGet("/", ListAsync).WithName("ListUsers");
        group.MapPost("/", CreateAsync).WithName("CreateUser").Validate<CreateUserRequest>();
        group.MapPut("/{userId:guid}", UpdateAsync).WithName("UpdateUser").Validate<UpdateUserRequest>();
        group.MapDelete("/{userId:guid}", DeleteAsync).WithName("DeleteUser");
        return admin;
    }

    private static async Task<Ok<List<CurrentUserResponse>>> ListAsync(UserManager<AdminUser> users, CancellationToken cancellationToken)
    {
        var all = await users.Users.OrderBy(u => u.Email).ToListAsync(cancellationToken);
        var responses = new List<CurrentUserResponse>();
        foreach (var user in all)
        {
            responses.Add(await AuthEndpoints.ToResponseAsync(user, users));
        }
        return TypedResults.Ok(responses);
    }

    private static async Task<Results<Created<CurrentUserResponse>, ValidationProblem>> CreateAsync(
        CreateUserRequest request, UserManager<AdminUser> users, IAuditLog audit, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var user = new AdminUser { UserName = request.Email, Email = request.Email, DisplayName = request.DisplayName, EmailConfirmed = true };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return TypedResults.ValidationProblem(created.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }
        await users.AddToRoleAsync(user, request.Role);
        audit.Record(new AuditRecord("user.created", "user", user.Id.ToString(), Details: $"email={user.Email}; role={request.Role}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/admin/v1/users/{user.Id}", await AuthEndpoints.ToResponseAsync(user, users));
    }

    private static async Task<Results<Ok<CurrentUserResponse>, NotFound>> UpdateAsync(
        Guid userId, UpdateUserRequest request, ClaimsPrincipal principal, UserManager<AdminUser> users, IAuditLog audit, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }
        var currentRoles = await users.GetRolesAsync(user);
        if (currentRoles.Contains(AdminRoles.Admin) && request.Role != AdminRoles.Admin)
        {
            await EnsureAnotherAdminExistsAsync(users, user);
        }
        user.DisplayName = request.DisplayName;
        await users.UpdateAsync(user);
        await users.RemoveFromRolesAsync(user, currentRoles);
        await users.AddToRoleAsync(user, request.Role);
        audit.Record(new AuditRecord("user.updated", "user", user.Id.ToString(), Details: $"role={request.Role}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await AuthEndpoints.ToResponseAsync(user, users));
    }

    private static async Task<Results<NoContent, NotFound>> DeleteAsync(
        Guid userId, ClaimsPrincipal principal, UserManager<AdminUser> users, IAuditLog audit, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        if (principal.FindFirstValue(ClaimTypes.NameIdentifier) == userId.ToString())
        {
            throw new DomainRuleException("user.self_delete", "You cannot delete your own account.");
        }
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return TypedResults.NotFound();
        }
        if (await users.IsInRoleAsync(user, AdminRoles.Admin))
        {
            await EnsureAnotherAdminExistsAsync(users, user);
        }
        await users.DeleteAsync(user);
        audit.Record(new AuditRecord("user.deleted", "user", user.Id.ToString(), Details: $"email={user.Email}"));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task EnsureAnotherAdminExistsAsync(UserManager<AdminUser> users, AdminUser user)
    {
        var admins = await users.GetUsersInRoleAsync(AdminRoles.Admin);
        if (admins.All(admin => admin.Id == user.Id))
        {
            throw new DomainRuleException("user.last_admin", "At least one administrator must remain.");
        }
    }
}
