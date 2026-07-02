using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
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

        group.MapPost("/{id:guid}/close", CloseAsync)
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("CloseEvent")
            .WithSummary("Close a published event and notify active registrations")
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

        group.MapGet("/{id:guid}/registrations", GetEventRegistrationsAsync)
            .RequireRole(UserRoles.Teacher, UserRoles.Admin)
            .WithName("GetEventRegistrations")
            .WithSummary("List confirmed registrations and the ordered waitlist for an organizer's event")
            .Produces<EventRegistrationsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

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

    private static async Task<Ok<List<EventResponse>>> GetAllAsync(
        ClaimsPrincipal user,
        IEventService eventService)
    {
        List<Event> events = await eventService.GetAllAsync(GetUserIdOrNull(user), GetRole(user));
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByStartDateAscAsync(
        ClaimsPrincipal user,
        IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.StartDateAscending, GetUserIdOrNull(user), GetRole(user));
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByStartDateDescAsync(
        ClaimsPrincipal user,
        IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.StartDateDescending, GetUserIdOrNull(user), GetRole(user));
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Ok<List<EventResponse>>> GetSortedByTitleAsync(
        ClaimsPrincipal user,
        IEventService eventService)
    {
        List<Event> events = await eventService.GetAllSortedAsync(EventListSort.TitleAscending, GetUserIdOrNull(user), GetRole(user));
        return TypedResults.Ok(events.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<EventResponse>, NotFound>> GetByIdAsync(
        Guid id,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        Event? ev = await eventService.GetByIdAsync(id, GetUserIdOrNull(user), GetRole(user));
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

        EventOperationResult<Event> result = await eventService.PublishAsync(id, organizerId, IsAdmin(user));
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

    private static async Task<Results<Ok<EventResponse>, BadRequest<ProblemHttpResult>, ForbidHttpResult, NotFound>> CloseAsync(
        Guid id,
        ClaimsPrincipal user,
        IEventService eventService)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        EventOperationResult<Event> result = await eventService.CloseAsync(id, organizerId, IsAdmin(user));
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

    private static async Task<Results<Ok<EventRegistrationsResponse>, ForbidHttpResult, NotFound>> GetEventRegistrationsAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        if (!TryGetUserId(user, out Guid organizerId))
        {
            return TypedResults.NotFound();
        }

        Event? ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null)
        {
            return TypedResults.NotFound();
        }

        if (!IsAdmin(user) && ev.OrganizerId != organizerId)
        {
            return TypedResults.Forbid();
        }

        List<RegistrationRosterItemResponse> confirmed = await dbContext.Registrations
            .AsNoTracking()
            .Include(registration => registration.Student)
            .Where(registration =>
                registration.EventId == id &&
                registration.RegistrationStatusId == 1 &&
                registration.CancelledAt == null)
            .OrderBy(registration => registration.RegisteredAt)
            .Select(registration => new RegistrationRosterItemResponse(
                registration.RegistrationId,
                registration.StudentId,
                registration.Student != null ? registration.Student.DisplayName : string.Empty,
                registration.Student != null ? registration.Student.Email : string.Empty,
                "Confirmed",
                null,
                registration.RegisteredAt))
            .ToListAsync();

        var waitlistedRegistrations = await dbContext.Registrations
            .AsNoTracking()
            .Include(registration => registration.Student)
            .Where(registration =>
                registration.EventId == id &&
                registration.RegistrationStatusId == 2 &&
                registration.CancelledAt == null)
            .OrderBy(registration => registration.RegisteredAt)
            .Select(registration => new
            {
                registration.RegistrationId,
                registration.StudentId,
                StudentName = registration.Student != null ? registration.Student.DisplayName : string.Empty,
                StudentEmail = registration.Student != null ? registration.Student.Email : string.Empty,
                registration.RegisteredAt
            })
            .ToListAsync();

        List<RegistrationRosterItemResponse> waitlistRows = waitlistedRegistrations
            .Select((registration, index) => new RegistrationRosterItemResponse(
                registration.RegistrationId,
                registration.StudentId,
                registration.StudentName,
                registration.StudentEmail,
                "Waitlisted",
                index + 1,
                registration.RegisteredAt))
            .ToList();

        return TypedResults.Ok(new EventRegistrationsResponse(
            id,
            ev.Capacity,
            confirmed.Count,
            waitlistRows.Count,
            confirmed,
            waitlistRows));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        return Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }

    private static Guid? GetUserIdOrNull(ClaimsPrincipal user) =>
        TryGetUserId(user, out Guid userId) ? userId : null;

    private static string GetRole(ClaimsPrincipal user) =>
        user.FindFirstValue(JwtClaimNames.Role) ?? UserRoles.Student;

    private static bool IsAdmin(ClaimsPrincipal user) =>
        string.Equals(GetRole(user), UserRoles.Admin, StringComparison.Ordinal);

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

public sealed record EventRegistrationsResponse(
    Guid EventId,
    int Capacity,
    int ConfirmedCount,
    int WaitlistCount,
    List<RegistrationRosterItemResponse> Confirmed,
    List<RegistrationRosterItemResponse> Waitlist);

public sealed record RegistrationRosterItemResponse(
    Guid RegistrationId,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    string RegistrationStatus,
    int? WaitlistPosition,
    DateTimeOffset RegisteredAt);
