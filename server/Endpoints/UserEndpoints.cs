using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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
            .WithName("GetUserById")
            .WithSummary("Get a user by id")
            .WithDescription("Returns a user's profile details without authentication secrets or internal password data.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
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
}

public sealed record UserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);
