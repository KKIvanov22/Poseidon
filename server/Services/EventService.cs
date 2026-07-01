using Microsoft.EntityFrameworkCore;
using Npgsql;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;

namespace Poseidon.Server.Services;

public enum EventListSort
{
    StartDateAscending,
    StartDateDescending,
    TitleAscending
}

public interface IEventService
{
    Task<EventOperationResult<Event>> CreateAsync(Guid organizerId, EventDetails details);
    Task<List<Event>> GetAllAsync(Guid? viewerId, string viewerRole);
    Task<List<Event>> GetAllSortedAsync(EventListSort sort, Guid? viewerId, string viewerRole);
    Task<List<Event>> GetByOrganizerAsync(Guid organizerId);
    Task<Event?> GetByIdAsync(Guid id, Guid? viewerId, string viewerRole);
    Task<EventOperationResult<Event>> UpdateAsync(Guid id, Guid organizerId, EventDetails details);
    Task<EventOperationResult<Event>> PublishAsync(Guid id, Guid organizerId, bool isAdmin = false);
    Task<EventOperationResult<Event>> CancelAsync(Guid id, Guid organizerId);
    Task<EventOperationResult<RegistrationResult>> RegisterAsync(Guid eventId, Guid studentId);
}

public sealed class EventService(
    AppDbContext dbContext,
    IRegistrationOrchestrator registrationOrchestrator) : IEventService
{
    private const int DraftStatusId = 1;
    private const int PublishedStatusId = 2;
    private const int CancelledStatusId = 3;

    public async Task<EventOperationResult<Event>> CreateAsync(Guid organizerId, EventDetails details)
    {
        string? validationError = Validate(details);
        if (validationError is not null)
        {
            return EventOperationResult<Event>.BadRequest(validationError);
        }

        var newEvent = new Event
        {
            OrganizerId = organizerId,
            EventStatusId = DraftStatusId,
            Title = details.Title.Trim(),
            Description = details.Description?.Trim(),
            StartsAt = details.StartsAt,
            EndsAt = details.EndsAt,
            Capacity = details.Capacity,
            LocationText = details.LocationText?.Trim()
        };

        dbContext.Events.Add(newEvent);
        await dbContext.SaveChangesAsync();

        return EventOperationResult<Event>.Success(newEvent);
    }

    public Task<List<Event>> GetAllAsync(Guid? viewerId, string viewerRole) =>
        GetAllSortedAsync(EventListSort.StartDateDescending, viewerId, viewerRole);

    public Task<List<Event>> GetAllSortedAsync(EventListSort sort, Guid? viewerId, string viewerRole)
    {
        IQueryable<Event> query = ApplyVisibility(dbContext.Events.AsNoTracking(), viewerId, viewerRole);

        query = sort switch
        {
            EventListSort.StartDateAscending => query.OrderBy(e => e.StartsAt),
            EventListSort.StartDateDescending => query.OrderByDescending(e => e.StartsAt),
            EventListSort.TitleAscending => query.OrderBy(e => e.Title),
            _ => query.OrderByDescending(e => e.StartsAt)
        };

        return query.ToListAsync();
    }

    public Task<List<Event>> GetByOrganizerAsync(Guid organizerId)
    {
        return dbContext.Events
            .AsNoTracking()
            .Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.StartsAt)
            .ToListAsync();
    }

    public Task<Event?> GetByIdAsync(Guid id, Guid? viewerId, string viewerRole)
    {
        return ApplyVisibility(dbContext.Events.AsNoTracking(), viewerId, viewerRole)
            .FirstOrDefaultAsync(e => e.EventId == id);
    }

    public async Task<EventOperationResult<Event>> UpdateAsync(Guid id, Guid organizerId, EventDetails details)
    {
        Event? ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
        {
            return EventOperationResult<Event>.NotFound();
        }

        if (ev.OrganizerId != organizerId)
        {
            return EventOperationResult<Event>.Forbidden();
        }

        if (ev.EventStatusId != DraftStatusId)
        {
            return EventOperationResult<Event>.BadRequest("Only draft events can be modified.");
        }

        string? validationError = Validate(details);
        if (validationError is not null)
        {
            return EventOperationResult<Event>.BadRequest(validationError);
        }

        ev.Title = details.Title.Trim();
        ev.Description = details.Description?.Trim();
        ev.StartsAt = details.StartsAt;
        ev.EndsAt = details.EndsAt;
        ev.Capacity = details.Capacity;
        ev.LocationText = details.LocationText?.Trim();
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return EventOperationResult<Event>.Success(ev);
    }

    public async Task<EventOperationResult<Event>> PublishAsync(Guid id, Guid organizerId, bool isAdmin = false)
    {
        Event? ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
        {
            return EventOperationResult<Event>.NotFound();
        }

        if (!isAdmin && ev.OrganizerId != organizerId)
        {
            return EventOperationResult<Event>.Forbidden();
        }

        if (ev.EventStatusId != DraftStatusId)
        {
            return EventOperationResult<Event>.BadRequest("This event is already published or has been canceled.");
        }

        ev.EventStatusId = PublishedStatusId;
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return EventOperationResult<Event>.Success(ev);
    }

    public async Task<EventOperationResult<Event>> CancelAsync(Guid id, Guid organizerId)
    {
        Event? ev = await dbContext.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null)
        {
            return EventOperationResult<Event>.NotFound();
        }

        if (ev.OrganizerId != organizerId)
        {
            return EventOperationResult<Event>.Forbidden();
        }

        if (ev.EventStatusId == CancelledStatusId)
        {
            return EventOperationResult<Event>.BadRequest("This event has already been canceled.");
        }

        ev.EventStatusId = CancelledStatusId;
        ev.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync();
        return EventOperationResult<Event>.Success(ev);
    }

    public async Task<EventOperationResult<RegistrationResult>> RegisterAsync(Guid eventId, Guid studentId)
    {
        Event? ev = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (ev is null)
        {
            return EventOperationResult<RegistrationResult>.NotFound();
        }

        if (ev.EventStatusId != PublishedStatusId)
        {
            return EventOperationResult<RegistrationResult>.BadRequest("Only published events accept registrations.");
        }

        if (ev.EndsAt <= DateTimeOffset.UtcNow)
        {
            return EventOperationResult<RegistrationResult>.BadRequest("This event has already ended.");
        }

        bool alreadyRegistered = await dbContext.Registrations.AnyAsync(registration =>
            registration.EventId == eventId &&
            registration.StudentId == studentId &&
            registration.CancelledAt == null);

        if (alreadyRegistered)
        {
            return EventOperationResult<RegistrationResult>.Conflict("You are already registered for this event.");
        }

        try
        {
            (Guid registrationId, int statusId) =
                await registrationOrchestrator.RegisterStudentTransactionAsync(eventId, studentId);

            return EventOperationResult<RegistrationResult>.Success(
                new RegistrationResult(registrationId, eventId, studentId, statusId, DateTimeOffset.UtcNow));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return EventOperationResult<RegistrationResult>.Conflict("You are already registered for this event.");
        }
    }

    private static string? Validate(EventDetails details)
    {
        if (details.EndsAt <= details.StartsAt)
        {
            return "The event must end after it starts.";
        }

        if (details.Capacity < 1)
        {
            return "Capacity must contain at least 1 seat.";
        }

        return null;
    }

    private static IQueryable<Event> ApplyVisibility(IQueryable<Event> query, Guid? viewerId, string viewerRole)
    {
        if (string.Equals(viewerRole, "Admin", StringComparison.Ordinal))
        {
            return query;
        }

        if (string.Equals(viewerRole, "Teacher", StringComparison.Ordinal) && viewerId.HasValue)
        {
            return query.Where(e => e.OrganizerId == viewerId.Value);
        }

        return query.Where(e => e.EventStatusId == PublishedStatusId);
    }
}

public sealed record EventDetails(
    string Title,
    string? Description,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int Capacity,
    string? LocationText);

public sealed record RegistrationResult(
    Guid RegistrationId,
    Guid EventId,
    Guid StudentId,
    int RegistrationStatusId,
    DateTimeOffset RegisteredAt);

public enum EventOperationStatus
{
    Success,
    NotFound,
    Forbidden,
    BadRequest,
    Conflict
}

public sealed record EventOperationResult<T>(EventOperationStatus Status, T? Value = default, string? Problem = null)
{
    public static EventOperationResult<T> Success(T value) => new(EventOperationStatus.Success, value);
    public static EventOperationResult<T> NotFound() => new(EventOperationStatus.NotFound);
    public static EventOperationResult<T> Forbidden() => new(EventOperationStatus.Forbidden);
    public static EventOperationResult<T> BadRequest(string problem) => new(EventOperationStatus.BadRequest, Problem: problem);
    public static EventOperationResult<T> Conflict(string problem) => new(EventOperationStatus.Conflict, Problem: problem);
}
