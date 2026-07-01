using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
using Poseidon.Server.RateLimiting;

namespace Poseidon.Server.Endpoints;

public static class WaitlistEndpoints
{
    public static RouteGroupBuilder MapWaitlistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/events/{id:guid}/waitlist")
            .WithTags("Waitlist")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .RequireAuthorization();

        group.MapGet("/", GetWaitlistAsync)
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("GetEventWaitlist")
            .WithSummary("Retrieve the ordered waitlist for an organizer's event.")
            .Produces<List<WaitlistResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<List<WaitlistResponse>>, ForbidHttpResult, NotFound>> GetWaitlistAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .Where(e => e.EventId == id)
            .Select(e => new { e.OrganizerId })
            .SingleOrDefaultAsync();

        if (ev is null)
        {
            return TypedResults.NotFound();
        }

        if (!IsAdmin(user) && (!TryGetUserId(user, out Guid organizerId) || ev.OrganizerId != organizerId))
        {
            return TypedResults.Forbid();
        }

        var waitlistedRegistrations = await dbContext.Registrations
            .AsNoTracking()
            .Where(registration =>
                registration.EventId == id &&
                registration.RegistrationStatusId == 2 &&
                registration.CancelledAt == null)
            .OrderBy(registration => registration.RegisteredAt)
            .Select(registration => new
            {
                registration.RegistrationId,
                registration.StudentId,
                registration.RegisteredAt
            })
            .ToListAsync();

        List<WaitlistResponse> waitlist = waitlistedRegistrations
            .Select((registration, index) => new WaitlistResponse(
                registration.RegistrationId,
                registration.StudentId,
                index + 1,
                registration.RegisteredAt))
            .ToList();

        return TypedResults.Ok(waitlist);
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static bool IsAdmin(ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(JwtClaimNames.Role), UserRoles.Admin, StringComparison.Ordinal);
}

public sealed record WaitlistResponse(Guid RegistrationId, Guid StudentId, int Position, DateTimeOffset JoinedAt);
