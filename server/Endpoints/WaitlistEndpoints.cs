using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;

namespace Poseidon.Server.Endpoints;

public static class WaitlistEndpoints
{
    public static RouteGroupBuilder MapWaitlistEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/events/{id:guid}/waitlist")
            .WithTags("Waitlist")
            .RequireAuthorization();

        group.MapGet("/", GetWaitlistAsync)
            .WithName("GetEventWaitlist")
            .WithSummary("Retrieve the current waitlisted queue position details for an event.")
            .Produces<List<WaitlistResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<List<WaitlistResponse>>, NotFound>> GetWaitlistAsync(
        Guid id,
        AppDbContext dbContext)
    {
        // Confirm the event exists first
        var eventExists = await dbContext.Events.AnyAsync(e => e.EventId == id);
        if (!eventExists) return TypedResults.NotFound();

        // Querying registration skeleton records that fall into waitlist status
        // (Assuming a StatusId or boolean tracks waitlisting in your database schema)
        var waitlist = await dbContext.Notifications
            .Where(n => n.EventId == id && n.NotificationType == "RegistrationWaitlisted")
            .OrderBy(n => n.CreatedAt)
            .Select((n, index) => new WaitlistResponse(n.UserId, index + 1, n.CreatedAt))
            .ToListAsync();

        return TypedResults.Ok(waitlist);
    }
}

public sealed record WaitlistResponse(Guid UserId, int Position, DateTimeOffset JoinedAt);