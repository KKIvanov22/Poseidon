using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.Services;

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
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("CreateEvent")
            .WithSummary("Create a new event")
            .Accepts<CreateEventRequest>("application/json")
            .Produces<EventResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
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
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("PublishEvent")
            .WithSummary("Publish a draft event to make it public")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // BE11: POST /events/{id}/cancel
        group.MapPost("/{id:guid}/cancel", CancelAsync)
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("CancelEvent")
            .WithSummary("Cancel a scheduled event")
            .Produces<EventResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        // BE12: POST /events/{id}/register
        group.MapPost("/{id:guid}/register", RegisterAsync)
            .RequireRole(UserRoles.Student)
            .WithName("RegisterForEvent")
            .WithSummary("Register the current student for a published event")
            .Produces<RegistrationResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

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

    // BE12: POST /events/{id}/register — validation here, confirmed/waitlist assignment in RegistrationOrchestrator
    private static async Task<Results<Created<RegistrationResponse>, BadRequest<ProblemHttpResult>, NotFound, Conflict<ProblemHttpResult>>> RegisterAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext,
        IRegistrationOrchestrator registrationOrchestrator)
    {
        var ev = await dbContext.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
        {
            return TypedResults.NotFound();
        }

        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out Guid studentId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        if (ev.EventStatusId != 2)
        {
            return TypedResults.BadRequest(TypedResults.Problem("Only published events accept registrations."));
        }

        if (ev.EndsAt <= DateTimeOffset.UtcNow)
        {
            return TypedResults.BadRequest(TypedResults.Problem("This event has already ended."));
        }

        bool alreadyRegistered = await dbContext.Database
            .SqlQuery<bool>($"""
                SELECT EXISTS (
                    SELECT 1
                    FROM public.registrations
                    WHERE event_id = {id}
                      AND student_id = {studentId}
                      AND cancelled_at IS NULL
                ) AS "Value"
                """)
            .SingleAsync();

        if (alreadyRegistered)
        {
            return TypedResults.Conflict(TypedResults.Problem("You are already registered for this event."));
        }

        try
        {
            (Guid registrationId, int statusId) = await registrationOrchestrator.RegisterStudentTransactionAsync(id, studentId);

            var response = new RegistrationResponse(
                registrationId,
                id,
                studentId,
                statusId,
                MapRegistrationStatus(statusId),
                DateTimeOffset.UtcNow);

            return TypedResults.Created($"/events/{id}/registrations/{registrationId}", response);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return TypedResults.Conflict(TypedResults.Problem("You are already registered for this event."));
        }
    }

    private static string MapRegistrationStatus(int statusId) => statusId switch
    {
        1 => "Confirmed",
        2 => "Waitlisted",
        3 => "Cancelled",
        _ => "Unknown"
    };

    private static EventResponse MapToResponse(Event e) =>
        new(e.EventId, e.OrganizerId, e.EventStatusId, e.Title, e.Description, e.StartsAt, e.EndsAt, e.Capacity, e.LocationText, e.CreatedAt);
}

public sealed record CreateEventRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText);
public sealed record UpdateEventRequest(string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText);
public sealed record EventResponse(Guid EventId, Guid OrganizerId, int EventStatusId, string Title, string? Description, DateTimeOffset StartsAt, DateTimeOffset EndsAt, int Capacity, string? LocationText, DateTimeOffset CreatedAt);

public sealed record RegistrationResponse(
    Guid RegistrationId,
    Guid EventId,
    Guid StudentId,
    int RegistrationStatusId,
    string RegistrationStatus,
    DateTimeOffset RegisteredAt);
