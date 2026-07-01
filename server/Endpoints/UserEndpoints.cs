using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
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

        group.MapGet("/{userId:guid}", GetByIdAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("GetUserById")
            .WithSummary("Get a user by id")
            .WithDescription("Returns a user's profile details without authentication secrets or internal password data.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{userId:guid}/role", ChangeRoleAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("ChangeUserRole")
            .WithSummary("Change a student's role to teacher or organizer")
            .WithDescription("Allows an admin to promote a student account to Teacher. The organizer alias is accepted and stored as Teacher.")
            .Accepts<ChangeUserRoleRequest>("application/json")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<UserResponse>, NotFound>> GetByIdAsync(
        Guid userId,
        AppDbContext dbContext)
    {
        UserResponse? user = await dbContext.Users
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

        return user is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(user);
    }

    private static async Task<Results<Ok<UserResponse>, BadRequest<ProblemHttpResult>, NotFound>> ChangeRoleAsync(
        Guid userId,
        ChangeUserRoleRequest request,
        AppDbContext dbContext)
    {
        string requestedRole = NormalizeRoleName(request.Role);
        if (requestedRole != UserRoles.Teacher)
        {
            return TypedResults.BadRequest(TypedResults.Problem("Role must be Teacher or Organizer."));
        }

        var user = await dbContext.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.UserId == userId);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (!string.Equals(user.Role?.RoleName, UserRoles.Student, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Only student accounts can be promoted by this endpoint."));
        }

        var role = await dbContext.UserRoles
            .SingleOrDefaultAsync(role => role.RoleName == requestedRole);

        if (role is null)
        {
            return TypedResults.BadRequest(TypedResults.Problem("The requested role is not configured."));
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
                : normalizedRole;
    }
}

public sealed record UserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record ChangeUserRoleRequest(string Role);
