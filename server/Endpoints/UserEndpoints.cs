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
    private const int StudentRoleId = 1;
    private const int TeacherRoleId = 2;
    private const int AdminRoleId = 3;

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
            .WithName("GetUserById")
            .WithSummary("Get a user by id")
            .WithDescription("Returns a user's profile details without authentication secrets or internal password data.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{userId:guid}/role", UpdateRoleAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("UpdateUserRole")
            .WithSummary("Change a user between Student and Teacher (admin only)")
            .Accepts<UpdateUserRoleRequest>("application/json")
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

    private static async Task<Results<Ok<UserResponse>, BadRequest<ProblemHttpResult>, NotFound>> UpdateRoleAsync(
        Guid userId,
        UpdateUserRoleRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext)
    {
        if (!TryGetUserId(principal, out Guid adminUserId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        if (userId == adminUserId)
        {
            return TypedResults.BadRequest(TypedResults.Problem("You cannot change your own role."));
        }

        string normalizedRole = request.Role.Trim();
        if (!string.Equals(normalizedRole, UserRoles.Student, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedRole, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Role must be either Student or Teacher."));
        }

        User? user = await dbContext.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == userId);

        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (user.RoleId == AdminRoleId)
        {
            return TypedResults.BadRequest(TypedResults.Problem("Admin roles cannot be changed through this endpoint."));
        }

        int newRoleId = string.Equals(normalizedRole, UserRoles.Teacher, StringComparison.OrdinalIgnoreCase)
            ? TeacherRoleId
            : StudentRoleId;

        if (user.RoleId == newRoleId)
        {
            return TypedResults.Ok(MapToResponse(user));
        }

        user.RoleId = newRoleId;
        await dbContext.SaveChangesAsync();

        user.Role = await dbContext.UserRoles.FindAsync(newRoleId);
        return TypedResults.Ok(MapToResponse(user));
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
}

public sealed record UserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record UpdateUserRoleRequest(string Role);
