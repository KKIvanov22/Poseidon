using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Poseidon.Server.Auth;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.RateLimiting;
using Poseidon.Server.Services;

namespace Poseidon.Server.Endpoints;

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/events")
            .WithTags("Events")
            .RequireRateLimiting(RateLimitPolicies.Api)
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

        group.MapGet("/sorted/start-date-asc", GetSortedByStartDateAscAsync)
            .WithName("GetEventsSortedByStartDateAsc")
            .WithSummary("Get all events sorted by start date (earliest first)")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/sorted/start-date-desc", GetSortedByStartDateDescAsync)
            .WithName("GetEventsSortedByStartDateDesc")
            .WithSummary("Get all events sorted by start date (latest first)")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/sorted/title", GetSortedByTitleAsync)
            .WithName("GetEventsSortedByTitle")
            .WithSummary("Get all events sorted by title (A–Z)")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/mine", GetMineAsync)
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("GetMyEvents")
            .WithSummary("Get events created by the current user")
            .Produces<List<EventResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

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
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<Event> result = await eventService.CreateAsync(organizerId, ToDetails(request));
        if (result.Status == EventOperationStatus.BadRequest)
        {
            return TypedResults.BadRequest(TypedResults.Problem(result.Problem));
        }

        Event newEvent = result.Value!;
        return TypedResults.Created($"/events/{newEvent.EventId}", MapToResponse(newEvent));
    }

    private static async Task<Results<Ok<List<EventResponse>>, BadRequest<ProblemHttpResult>>> GetMineAsync(
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        List<Event> events = await eventService.GetByOrganizerAsync(organizerId);
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetAllAsync(IEventService eventService)
    {
        List<Event> events = await eventService.GetAllAsync();
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByStartDateAscAsync(IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.StartDateAscending);
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByStartDateDescAsync(IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.StartDateDescending);
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByTitleAsync(IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.TitleAscending);
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<EventResponse>, NotFound>> GetByIdAsync(
        Guid id,
        IEventService eventService)
    {
        Event? ev = await eventService.GetByIdAsync(id);
        return ev is null ? TypedResults.NotFound() : TypedResults.Ok(MapToResponse(ev));
    }

    // BE09: PUT /events/{id} - Update an existing event details
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> UpdateAsync(
        Guid id,
        UpdateEventRequest request,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<Event> result = await eventService.UpdateAsync(id, organizerId, ToDetails(request));
        return ToEventMutationHttpResult(result);
    }

    // BE10: POST /events/{id}/publish - Advance event status from Draft (1) to Published (2)
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> PublishAsync(
        Guid id,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<Event> result = await eventService.PublishAsync(id, organizerId);
        return ToEventMutationHttpResult(result);
    }

    // BE11: POST /events/{id}/cancel - Move state to Canceled (3)
    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> CancelAsync(
        Guid id,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<Event> result = await eventService.CancelAsync(id, organizerId);
        return ToEventMutationHttpResult(result);
    }

    // BE12: POST /events/{id}/register - registration assignment remains in RegistrationOrchestrator
    private static async Task<Results<Created<RegistrationResponse>, BadRequest<ProblemHttpResult>, NotFound, Conflict<ProblemHttpResult>>> RegisterAsync(
        Guid id,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid studentId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<RegistrationResult> result = await eventService.RegisterAsync(id, studentId);

        return result.Status switch
        {
            EventOperationStatus.Success => TypedResults.Created(
                $"/events/{id}/registrations/{result.Value!.RegistrationId}",
                MapToResponse(result.Value)),
            EventOperationStatus.BadRequest => TypedResults.BadRequest(TypedResults.Problem(result.Problem)),
            EventOperationStatus.NotFound => TypedResults.NotFound(),
            EventOperationStatus.Conflict => TypedResults.Conflict(TypedResults.Problem(result.Problem)),
            _ => TypedResults.BadRequest(TypedResults.Problem("Unable to register for this event."))
        };
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static EventDetails ToDetails(CreateEventRequest request) =>
        new(request.Title, request.Description, request.StartsAt, request.EndsAt, request.Capacity, request.LocationText);

    private static EventDetails ToDetails(UpdateEventRequest request) =>
        new(request.Title, request.Description, request.StartsAt, request.EndsAt, request.Capacity, request.LocationText);

    private static Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound> ToEventMutationHttpResult(
        EventOperationResult<Event> result)
    {
        return result.Status switch
        {
            EventOperationStatus.Success => TypedResults.Ok(MapToResponse(result.Value!)),
            EventOperationStatus.BadRequest => TypedResults.BadRequest(TypedResults.Problem(result.Problem)),
            EventOperationStatus.Forbidden => TypedResults.Forbid(),
            EventOperationStatus.NotFound => TypedResults.NotFound(),
            _ => TypedResults.BadRequest(TypedResults.Problem("Unable to update this event."))
        };
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

    private static RegistrationResponse MapToResponse(RegistrationResult registration) =>
        new(
            registration.RegistrationId,
            registration.EventId,
            registration.StudentId,
            registration.RegistrationStatusId,
            MapRegistrationStatus(registration.RegistrationStatusId),
            registration.RegisteredAt);
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
