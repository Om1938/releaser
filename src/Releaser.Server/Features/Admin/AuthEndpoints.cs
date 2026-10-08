using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Releaser.Server.Features.Audit;
using Releaser.Server.Infrastructure.Http;
using Releaser.Server.Infrastructure.Persistence;

namespace Releaser.Server.Features.Admin;

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(256);
    }
}

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordValidator()
    {
        RuleFor(r => r.CurrentPassword).NotEmpty();
        RuleFor(r => r.NewPassword).NotEmpty().MinimumLength(12).MaximumLength(256);
    }
}

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuth(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/auth").WithTags("Authentication");
        group.MapPost("/login", LoginAsync).WithName("Login").AllowAnonymous().Validate<LoginRequest>().RequireRateLimiting(RateLimiting.LoginPolicy);
        group.MapPost("/logout", LogoutAsync).WithName("Logout");
        group.MapGet("/me", MeAsync).WithName("GetCurrentUser");
        group.MapPost("/password", ChangePasswordAsync).WithName("ChangePassword").Validate<ChangePasswordRequest>();
        return admin;
    }

    private static async Task<Results<Ok<CurrentUserResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, SignInManager<AdminUser> signIn, UserManager<AdminUser> users, IAuditLog audit, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        var result = user is null
            ? SignInResult.Failed
            : await signIn.PasswordSignInAsync(user, request.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed",
                detail: result.IsLockedOut ? "The account is temporarily locked." : "Invalid email or password.");
        }
        audit.Record(new AuditRecord("auth.signed_in", "user", user!.Id.ToString(), Details: user.Email));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await ToResponseAsync(user, users));
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<AdminUser> signIn)
    {
        await signIn.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult>> MeAsync(ClaimsPrincipal principal, UserManager<AdminUser> users)
    {
        var user = await users.GetUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(await ToResponseAsync(user, users));
    }

    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, ClaimsPrincipal principal, UserManager<AdminUser> users, SignInManager<AdminUser> signIn,
        IAuditLog audit, ReleaserDbContext db, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return TypedResults.ValidationProblem(result.Errors.ToDictionary(e => e.Code, e => new[] { e.Description }));
        }
        await signIn.RefreshSignInAsync(user);
        audit.Record(new AuditRecord("user.password_changed", "user", user.Id.ToString()));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static async Task<CurrentUserResponse> ToResponseAsync(AdminUser user, UserManager<AdminUser> users) =>
        new(user.Id, user.Email!, user.DisplayName, [.. await users.GetRolesAsync(user)]);
}
