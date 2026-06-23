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
            .RequireAuthorization(); 

        // POST /events
        group.MapPost("/", CreateAsync)
            .WithName("CreateEvent")
            .WithSummary("Create a new event")
            .Accepts<CreateEventRequest>("application/json")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        // GET /events
        group.MapGet("/", GetAllAsync)
            .WithName("GetAllEvents")
            .WithSummary("Get all events")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        // GET /events/{id}
        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetEventById")
            .WithSummary("Get an event by id")
            .Produces<EventResponse>(StatusCodes.Status200OK)
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
            EventStatusId = 1, 
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

    private static EventResponse MapToResponse(Event e) =>
        new(e.EventId, e.OrganizerId, e.EventStatusId, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Capacity, e.LocationText, e.CreatedAt);
}

public sealed record CreateEventRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText);
public sealed record EventResponse(Guid EventId, Guid OrganizerId, int EventStatusId, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText, DateTimeOffset CreatedAt);