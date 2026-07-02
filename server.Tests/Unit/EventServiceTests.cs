using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.Services;
using Xunit;

namespace Poseidon.Server.Tests.Unit;

public sealed class EventServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidDetails_CreatesDraftEventWithTrimmedText()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());
        Guid organizerId = Guid.NewGuid();

        EventOperationResult<Event> result = await service.CreateAsync(
            organizerId,
            ValidDetails(title: "  Workshop  ", description: "  Intro  ", locationText: "  Room 9  "));

        Assert.Equal(EventOperationStatus.Success, result.Status);
        Assert.Equal(1, result.Value!.EventStatusId);
        Assert.Equal("Workshop", result.Value.Title);
        Assert.Equal("Intro", result.Value.Description);
        Assert.Equal("Room 9", result.Value.LocationText);
        Assert.Equal(organizerId, result.Value.OrganizerId);
        Assert.Equal(1, await dbContext.Events.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_EndBeforeStart_ReturnsBadRequestAndDoesNotPersist()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());
        DateTimeOffset startsAt = DateTimeOffset.UtcNow.AddDays(2);

        EventOperationResult<Event> result = await service.CreateAsync(
            Guid.NewGuid(),
            ValidDetails(startsAt: startsAt, endsAt: startsAt));

        Assert.Equal(EventOperationStatus.BadRequest, result.Status);
        Assert.Equal("The event must end after it starts.", result.Problem);
        Assert.Empty(await dbContext.Events.ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_CapacityBelowOne_ReturnsBadRequestAndDoesNotPersist()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.CreateAsync(
            Guid.NewGuid(),
            ValidDetails(capacity: 0));

        Assert.Equal(EventOperationStatus.BadRequest, result.Status);
        Assert.Equal("Capacity must contain at least 1 seat.", result.Problem);
        Assert.Empty(await dbContext.Events.ToListAsync());
    }

    [Fact]
    public async Task UpdateAsync_DifferentOrganizer_ReturnsForbiddenAndLeavesEventUnchanged()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid originalOrganizerId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, originalOrganizerId, "Original", statusId: 1);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.UpdateAsync(
            existing.EventId,
            Guid.NewGuid(),
            ValidDetails(title: "Changed"));

        Assert.Equal(EventOperationStatus.Forbidden, result.Status);
        Assert.Equal("Original", (await dbContext.Events.SingleAsync()).Title);
    }

    [Fact]
    public async Task PublishAsync_AdminCanPublishEventOwnedByAnotherOrganizer()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Event existing = AddEvent(dbContext, Guid.NewGuid(), "Draft", statusId: 1);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.PublishAsync(
            existing.EventId,
            Guid.NewGuid(),
            isAdmin: true);


        Assert.Equal(EventOperationStatus.Success, result.Status);
        Assert.Equal(2, result.Value!.EventStatusId);
        Assert.NotNull(result.Value.UpdatedAt);
    }

    [Fact]
    public async Task GetAllSortedAsync_StudentSeesOnlyPublishedEvents()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        AddEvent(dbContext, Guid.NewGuid(), "Draft", statusId: 1);
        Event published = AddEvent(dbContext, Guid.NewGuid(), "Published", statusId: 2);
        AddEvent(dbContext, Guid.NewGuid(), "Completed", statusId: 4);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        List<Event> events = await service.GetAllSortedAsync(
            EventListSort.StartDateDescending,
            Guid.NewGuid(),
            "Student");

        Event onlyEvent = Assert.Single(events);
        Assert.Equal(published.EventId, onlyEvent.EventId);
    }

    [Fact]
    public async Task GetAllSortedAsync_AdminSeesAllEventsSortedByTitle()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Event beta = AddEvent(dbContext, Guid.NewGuid(), "Beta", statusId: 2);
        Event alpha = AddEvent(dbContext, Guid.NewGuid(), "Alpha", statusId: 1);
        Event gamma = AddEvent(dbContext, Guid.NewGuid(), "Gamma", statusId: 3);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        List<Event> events = await service.GetAllSortedAsync(
            EventListSort.TitleAscending,
            Guid.NewGuid(),
            "Admin");

        Assert.Equal([alpha.EventId, beta.EventId, gamma.EventId], events.Select(e => e.EventId).ToArray());
    }

    [Fact]
    public async Task CancelAsync_CancelsActiveRegistrationsAndEnqueuesNotifications()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid organizerId = Guid.NewGuid();
        Guid confirmedStudentId = Guid.NewGuid();
        Guid waitlistedStudentId = Guid.NewGuid();
        Guid cancelledStudentId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, organizerId, "Published", statusId: 2);
        dbContext.Registrations.AddRange(
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = confirmedStudentId,
                RegistrationStatusId = 1,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            },
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = waitlistedStudentId,
                RegistrationStatusId = 2,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = cancelledStudentId,
                RegistrationStatusId = 3,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CancelledAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.CancelAsync(existing.EventId, organizerId);

        List<Registration> registrations = await dbContext.Registrations.ToListAsync();
        Assert.Equal(EventOperationStatus.Success, result.Status);
        Assert.Equal(3, result.Value!.EventStatusId);
        Assert.All(
            registrations.Where(registration => registration.StudentId != cancelledStudentId),
            registration =>
            {
                Assert.Equal(3, registration.RegistrationStatusId);
                Assert.NotNull(registration.CancelledAt);
            });
        Assert.Equal(2, await dbContext.NotificationJobs.CountAsync());
        Assert.All(await dbContext.NotificationJobs.ToListAsync(), job =>
        {
            Assert.Equal(1, job.JobStatusId);
            Assert.Contains("EventCancelled", job.Payload);
        });
    }

    [Fact]
    public async Task CloseAsync_CompletesPublishedEventAndNotifiesActiveRegistrations()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid organizerId = Guid.NewGuid();
        Guid confirmedStudentId = Guid.NewGuid();
        Guid waitlistedStudentId = Guid.NewGuid();
        Guid cancelledStudentId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, organizerId, "Published", statusId: 2);
        dbContext.Registrations.AddRange(
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = confirmedStudentId,
                RegistrationStatusId = 1,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            },
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = waitlistedStudentId,
                RegistrationStatusId = 2,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            },
            new Registration
            {
                RegistrationId = Guid.NewGuid(),
                EventId = existing.EventId,
                StudentId = cancelledStudentId,
                RegistrationStatusId = 3,
                RegisteredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CancelledAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.CloseAsync(existing.EventId, organizerId);

        List<Registration> registrations = await dbContext.Registrations.ToListAsync();
        Assert.Equal(EventOperationStatus.Success, result.Status);
        Assert.Equal(4, result.Value!.EventStatusId);
        Assert.All(
            registrations.Where(registration => registration.StudentId != cancelledStudentId),
            registration => Assert.Null(registration.CancelledAt));
        Assert.Equal(2, await dbContext.NotificationJobs.CountAsync());
        Assert.All(await dbContext.NotificationJobs.ToListAsync(), job =>
        {
            Assert.Equal(1, job.JobStatusId);
            Assert.Contains("EventCompleted", job.Payload);
        });
    }

    [Fact]
    public async Task RegisterAsync_AlreadyActivelyRegistered_ReturnsConflictWithoutOrchestrating()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid studentId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, Guid.NewGuid(), "Published", statusId: 2);
        dbContext.Registrations.Add(new Registration
        {
            RegistrationId = Guid.NewGuid(),
            EventId = existing.EventId,
            StudentId = studentId,
            RegistrationStatusId = 1,
            RegisteredAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
        var orchestrator = new FakeRegistrationOrchestrator();
        var service = new EventService(dbContext, orchestrator);

        EventOperationResult<RegistrationResult> result = await service.RegisterAsync(existing.EventId, studentId);

        Assert.Equal(EventOperationStatus.Conflict, result.Status);
        Assert.Equal("You are already registered for this event.", result.Problem);
        Assert.Equal(0, orchestrator.RegisterCallCount);
    }

    [Fact]
    public async Task CreateAsync_ZeroCapacity_ReturnsBadRequestAndDoesNotPersist()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.CreateAsync(
            Guid.NewGuid(),
            ValidDetails(capacity: 0));

        Assert.Equal(EventOperationStatus.BadRequest, result.Status);
        Assert.Equal("Capacity must contain at least 1 seat.", result.Problem);
        Assert.Equal(0, await dbContext.Events.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_PublishedEvent_ReturnsBadRequestAndLeavesEventUnchanged()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid organizerId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, organizerId, "Published", statusId: 2);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        EventOperationResult<Event> result = await service.UpdateAsync(
            existing.EventId,
            organizerId,
            ValidDetails(title: "Changed"));

        Event savedEvent = await dbContext.Events.SingleAsync();
        Assert.Equal(EventOperationStatus.BadRequest, result.Status);
        Assert.Equal("Only draft events can be modified.", result.Problem);
        Assert.Equal("Published", savedEvent.Title);
        Assert.Null(savedEvent.UpdatedAt);
    }

    [Fact]
    public async Task GetAllSortedAsync_StudentSeesPublishedEventsOnlyOrderedByTitle()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        AddEvent(dbContext, Guid.NewGuid(), "Zoo", statusId: 2);
        AddEvent(dbContext, Guid.NewGuid(), "Draft", statusId: 1);
        AddEvent(dbContext, Guid.NewGuid(), "Alpha", statusId: 2);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        List<Event> events = await service.GetAllSortedAsync(EventListSort.TitleAscending, null, "Student");

        Assert.Equal(new[] { "Alpha", "Zoo" }, events.Select(e => e.Title));
    }

    [Fact]
    public async Task GetByIdAsync_TeacherCannotSeeAnotherOrganizerEvent()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid viewerId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, Guid.NewGuid(), "Other teacher event", statusId: 2);
        await dbContext.SaveChangesAsync();
        var service = new EventService(dbContext, new FakeRegistrationOrchestrator());

        Event? result = await service.GetByIdAsync(existing.EventId, viewerId, "Teacher");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_PublishedEvent_ReturnsOrchestratedRegistration()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        Guid registrationId = Guid.NewGuid();
        Guid studentId = Guid.NewGuid();
        Event existing = AddEvent(dbContext, Guid.NewGuid(), "Published", statusId: 2);
        await dbContext.SaveChangesAsync();
        var orchestrator = new FakeRegistrationOrchestrator(registrationId, statusId: 2);
        var service = new EventService(dbContext, orchestrator);

        EventOperationResult<RegistrationResult> result = await service.RegisterAsync(existing.EventId, studentId);

        Assert.Equal(EventOperationStatus.Success, result.Status);
        Assert.Equal(1, orchestrator.RegisterCallCount);
        Assert.Equal(registrationId, result.Value!.RegistrationId);
        Assert.Equal(existing.EventId, result.Value.EventId);
        Assert.Equal(studentId, result.Value.StudentId);
        Assert.Equal(2, result.Value.RegistrationStatusId);
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static EventDetails ValidDetails(
        string title = "Event",
        string? description = "Description",
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null,
        int capacity = 10,
        string? locationText = "Auditorium")
    {
        DateTimeOffset start = startsAt ?? DateTimeOffset.UtcNow.AddDays(1);
        return new EventDetails(title, description, start, endsAt ?? start.AddHours(2), capacity, locationText);
    }

    private static Event AddEvent(AppDbContext dbContext, Guid organizerId, string title, int statusId)
    {
        var ev = new Event
        {
            EventId = Guid.NewGuid(),
            OrganizerId = organizerId,
            EventStatusId = statusId,
            Title = title,
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            Capacity = 5,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Events.Add(ev);
        return ev;
    }

    private sealed class FakeRegistrationOrchestrator(Guid? registrationId = null, int statusId = 1) : IRegistrationOrchestrator
    {
        public int RegisterCallCount { get; private set; }

        public Task<(Guid RegistrationId, int StatusId)> RegisterStudentTransactionAsync(Guid eventId, Guid studentId)
        {
            RegisterCallCount++;
            return Task.FromResult((registrationId ?? Guid.NewGuid(), statusId));
        }

        public Task<CancelRegistrationTransactionResult> CancelRegistrationTransactionAsync(Guid eventId, Guid studentId) =>
            Task.FromResult(CancelRegistrationTransactionResult.NotFound());
    }
}
