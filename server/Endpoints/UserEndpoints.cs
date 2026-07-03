using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.RateLimiting;

namespace Poseidon.Server.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/users")
            .WithTags("Users")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .RequireAuthorization();

        group.MapGet("/me", GetMeAsync)
            .WithName("GetCurrentUser")
            .WithSummary("Get the authenticated user's profile")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", ListAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("ListUsers")
            .WithSummary("List all users (admin only)")
            .Produces<List<UserResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapGet("/{userId:guid}", GetByIdAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("GetUserById")
            .WithSummary("Get a user by id")
            .WithDescription("Returns a user's profile details without authentication secrets or internal password data.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/me/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            .WithSummary("Change the authenticated user's password")
            .Accepts<ChangePasswordRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{userId:guid}/role", ChangeRoleAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("ChangeUserRole")
            .WithSummary("Change a user's role between student and teacher")
            .WithDescription("Allows an admin to promote a student account to Teacher or demote a teacher account to Student. The organizer alias is accepted and stored as Teacher.")
            .Accepts<ChangeUserRoleRequest>("application/json")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<UserResponse>, NotFound>> GetMeAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext)
    {
        if (!TryGetUserId(principal, out Guid userId))
        {
            return TypedResults.NotFound();
        }
        

        UserResponse? user = await QueryUserResponse(dbContext, userId);
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
    }

    private static async Task<Ok<List<UserResponse>>> ListAsync(AppDbContext dbContext)
    {
        List<UserResponse> users = await dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .OrderBy(user => user.DisplayName)
            .Select(user => new UserResponse(
                user.UserId,
                user.Email,
                user.DisplayName,
                user.Role != null ? user.Role.RoleName : "Unknown",
                user.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(users);
    }

    private static async Task<Results<Ok<UserResponse>, NotFound>> GetByIdAsync(
        Guid userId,
        AppDbContext dbContext)
    {
        UserResponse? user = await QueryUserResponse(dbContext, userId);
        return user is null ? TypedResults.NotFound() : TypedResults.Ok(user);
    }

    private static async Task<UserResponse?> QueryUserResponse(AppDbContext dbContext, Guid userId)
    {
        return await dbContext.Users
            .AsNoTracking()
            .Include(user => user.Role)
            .Where(user => user.UserId == userId)
            .Select(user => new UserResponse(
                user.UserId,
                user.Email,
                user.DisplayName,
                user.Role != null ? user.Role.RoleName : "Unknown",
                user.CreatedAt))
            .SingleOrDefaultAsync();
    }

    private static UserResponse MapToResponse(User user) =>
        new(
            user.UserId,
            user.Email,
            user.DisplayName,
            user.Role?.RoleName ?? "Unknown",
            user.CreatedAt);

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static async Task<Results<NoContent, ProblemHttpResult, NotFound>> ChangePasswordAsync(
        ClaimsPrincipal principal,
        ChangePasswordRequest request,
        AppDbContext dbContext)
    {
        if (!TryGetUserId(principal, out Guid userId))
        {
            return TypedResults.NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return TypedResults.Problem(detail: "Current password and new password are required.");
        }

        // no minimum password length enforced here; allow any length

        User? user = await dbContext.Users.SingleOrDefaultAsync(user => user.UserId == userId);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return TypedResults.Problem(detail: "Current password is incorrect.");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await dbContext.SaveChangesAsync();

        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult, NotFound>> ChangeRoleAsync(
        Guid userId,
        ChangeUserRoleRequest request,
        AppDbContext dbContext)
    {
        string requestedRole = NormalizeRoleName(request.Role);
        if (requestedRole is not (UserRoles.Student or UserRoles.Teacher))
        {
            return TypedResults.Problem(detail: "Role must be Student, Teacher, or Organizer.");
        }

        var user = await dbContext.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.UserId == userId);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (string.Equals(user.Role?.RoleName, UserRoles.Admin, StringComparison.Ordinal))
        {
            return TypedResults.Problem(detail: "Admin accounts cannot be changed by this endpoint.");
        }

        if (string.Equals(user.Role?.RoleName, requestedRole, StringComparison.Ordinal))
        {
            return TypedResults.Ok(MapToResponse(user));
        }

        var role = await dbContext.UserRoles
            .SingleOrDefaultAsync(role => role.RoleName == requestedRole);

        if (role is null)
        {
            return TypedResults.Problem(detail: "The requested role is not configured.");
        }

        user.RoleId = role.RoleId;
        user.Role = role;
        await dbContext.SaveChangesAsync();

        return TypedResults.Ok(new UserResponse(
            user.UserId,
            user.Email,
            user.DisplayName,
            role.RoleName,
            user.CreatedAt));
    }

    private static string NormalizeRoleName(string role)
    {
        string normalizedRole = role.Trim();
        return normalizedRole.Equals("Organizer", StringComparison.OrdinalIgnoreCase)
            ? UserRoles.Teacher
            : normalizedRole.Equals(UserRoles.Teacher, StringComparison.OrdinalIgnoreCase)
                ? UserRoles.Teacher
                : normalizedRole.Equals(UserRoles.Student, StringComparison.OrdinalIgnoreCase)
                    ? UserRoles.Student
                    : normalizedRole;
    }
}

public sealed record UserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ChangeUserRoleRequest(string Role);
