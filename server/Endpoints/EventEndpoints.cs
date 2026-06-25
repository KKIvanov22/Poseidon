using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/events")
            .WithTags("Events")
            .RequireAuthorization(); // Secures all routes under /events with JWT auth

        // BE06: POST /events
        group.MapPost("/", CreateAsync)
            .WithName("CreateEvent")
            .WithSummary("Create a new event")
            .Accepts<CreateEventRequest>("application/json")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // BE07: GET /events
        group.MapGet("/", GetAllAsync)
            .WithName("GetAllEvents")
            .WithSummary("Get all events")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // BE08: GET /events/{id}
        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetEventById")
            .WithSummary("Get an event by id")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        // BE09: PUT /events/{id}
        group.MapPut("/{id:guid}", UpdateAsync)
            .WithName("UpdateEvent")
            .WithSummary("Update an existing draft event")
            .Accepts<UpdateEventRequest>("application/json")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // BE10: POST /events/{id}/publish
        group.MapPost("/{id:guid}/publish", PublishAsync)
            .WithName("PublishEvent")
            .WithSummary("Publish a draft event to make it public")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // BE11: POST /events/{id}/cancel
        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .WithName("CancelEvent")
            .WithSummary("Cancel a scheduled event")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // BE12: POST /events/{id}/register (Skeleton)
        group.MapPost("/{id:guid}/register", RegisterSkeletonAsync)
            .WithName("RegisterForEventSkeleton")
            .WithSummary("Skeleton endpoint for joining an event")
            .Produces<object>(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Created<EventResponse>, BadRequest<ProblemHttpResult>>> CreateAsync(
        CreateEventRequest request,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        string? nameIdentifier = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(nameIdentifier, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        if (request.EndsAt <= request.StartsAt)
        {
            return TypedResults.BadRequest(TypedResults.Problem("The event must end after it starts."));
        }
        if (request.Capacity < 1)
        {
            return TypedResults.BadRequest(TypedResults.Problem("Capacity must contain at least 1 seat."));
        }

        var newEvent = new Event
        {
            OrganizerId = organizerId,
            EventStatusId = 1, // 1 = Draft
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Capacity = request.Capacity,
            LocationText = request.LocationText?.Trim()
        };

        dbContext.Events.Add(newEvent);
        await dbContext.SaveChangesAsync();

        return TypedResults.Created($"/events/{newEvent.EventId}", MapToResponse(newEvent));
    }

    private static async Task<Ok<List<EventResponse>>> GetAllAsync(AppDbContext dbContext)
    {
        var events = await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(e => e.StartsAt)
            .Select(e => MapToResponse(e))
            .ToListAsync();

        return TypedResults.Ok(events);
    }

    private static async Task<Results<Ok<EventResponse>, NotFound>> GetByIdAsync(
        Guid id,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        return ev is null ? TypedResults.NotFound() : TypedResults.Ok(MapToResponse(ev));
    }

    // BE09: PUT /events/{id} - Update an existing event details
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return TypedResults.NotFound();

        // Security check: Only the original creator can modify this event
        string? currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ev.OrganizerId.ToString() != currentUserId) return TypedResults.Forbid();

        // Rule check: Prevent changes if the event isn't a Draft anymore
        if (ev.EventStatusId != 1)
        {
            return TypedResults.BadRequest(TypedResults.Problem("Only draft events can be modified."));
        }

        if (request.EndsAt <= request.StartsAt)
        {
            return TypedResults.BadRequest(TypedResults.Problem("The event must end after it starts."));
        }

        // Apply changes cleanly
        ev.Title = request.Title.Trim();
        ev.Description = request.Description?.Trim();
        ev.StartsAt = request.StartsAt;
        ev.EndsAt = request.EndsAt;
        ev.Capacity = request.Capacity;
        ev.LocationText = request.LocationText?.Trim();
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return TypedResults.Ok(MapToResponse(ev));
    }

    // BE10: POST /events/{id}/publish - Advance event status from Draft (1) to Published (2)
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> PublishAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return TypedResults.NotFound();

        string? currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ev.OrganizerId.ToString() != currentUserId) return TypedResults.Forbid();

        if (ev.EventStatusId != 1)
        {
            return TypedResults.BadRequest(TypedResults.Problem("This event is already published or has been canceled."));
        }

        ev.EventStatusId = 2; // 2 = Published / Live
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return TypedResults.Ok(MapToResponse(ev));
    }

    // BE11: POST /events/{id}/cancel - Move state to Canceled (3)
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> CancelAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return TypedResults.NotFound();

        string? currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (ev.OrganizerId.ToString() != currentUserId) return TypedResults.Forbid();

        if (ev.EventStatusId == 3)
        {
            return TypedResults.BadRequest(TypedResults.Problem("This event has already been canceled."));
        }

        ev.EventStatusId = 3; // 3 = Canceled
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return TypedResults.Ok(MapToResponse(ev));
    }

    // BE12: POST /events/{id}/register - Baseline skeleton placeholder
    private static async Task<Results<Accepted<object>, NotFound>> RegisterSkeletonAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var ev = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return TypedResults.NotFound();

        string? userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        // Skeleton tracking output response to let front-end developers mock user signups
        var skeletonPayload = new
        {
            Message = "Skeleton Registration Hook Success.",
            EventId = id,
            RegisteredUserId = userId,
            Timestamp = DateTimeOffset.UtcNow,
            Status = "PendingCapacityVerification"
        };

        return TypedResults.Accepted($"/events/{id}/registration-status", (object)skeletonPayload);
    }

    private static EventResponse MapToResponse(Event e) =>
        new(e.EventId, e.OrganizerId, e.EventStatusId, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Capacity, e.LocationText, e.CreatedAt);
}

public sealed record CreateEventRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText);
public sealed record UpdateEventRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText);
public sealed record EventResponse(Guid EventId, Guid OrganizerId, int EventStatusId, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText, DateTimeOffset CreatedAt);