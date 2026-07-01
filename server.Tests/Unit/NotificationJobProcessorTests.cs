using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.Services.Notifications;
using Xunit;

namespace Poseidon.Server.Tests.Unit;

public sealed class NotificationJobProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MissingJob_ReturnsNotFound()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        var processor = new NotificationJobProcessor(database.DbContext, new FakeEmailNotificationSender());

        NotificationJobProcessResult result = await processor.ProcessAsync(Guid.NewGuid());

        Assert.Equal(NotificationJobProcessStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ProcessAsync_NonPendingJob_ReturnsNotPendingAndDoesNotSendEmail()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        NotificationJob job = SeedNotificationJob(database.DbContext, jobStatusId: 2);
        await database.DbContext.SaveChangesAsync();
        var sender = new FakeEmailNotificationSender();
        var processor = new NotificationJobProcessor(database.DbContext, sender);

        NotificationJobProcessResult result = await processor.ProcessAsync(job.NotificationJobId);

        Assert.Equal(NotificationJobProcessStatus.NotPending, result.Status);
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedPayload_ReturnsUnsupportedTypeAndLeavesJobPending()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        NotificationJob job = SeedNotificationJob(
            database.DbContext,
            payload: """{"type":"Unknown","event_id":"00000000-0000-0000-0000-000000000001","student_id":"00000000-0000-0000-0000-000000000002"}""");
        await database.DbContext.SaveChangesAsync();
        var processor = new NotificationJobProcessor(database.DbContext, new FakeEmailNotificationSender());

        NotificationJobProcessResult result = await processor.ProcessAsync(job.NotificationJobId);

        Assert.Equal(NotificationJobProcessStatus.UnsupportedType, result.Status);
        Assert.Equal(1, (await database.DbContext.NotificationJobs.SingleAsync()).JobStatusId);
    }

    [Fact]
    public async Task ProcessAsync_ValidPendingJob_SendsEmailMarksSucceededAndAddsDelivery()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        NotificationJob job = SeedNotificationJob(database.DbContext);
        await database.DbContext.SaveChangesAsync();
        var sender = new FakeEmailNotificationSender();
        var processor = new NotificationJobProcessor(database.DbContext, sender);

        NotificationJobProcessResult result = await processor.ProcessAsync(job.NotificationJobId);

        NotificationJob savedJob = await database.DbContext.NotificationJobs.SingleAsync();
        NotificationDelivery delivery = await database.DbContext.NotificationDeliveries.SingleAsync();
        Assert.Equal(NotificationJobProcessStatus.Succeeded, result.Status);
        Assert.Single(sender.Sent);
        Assert.Equal(3, savedJob.JobStatusId);
        Assert.Equal(1, savedJob.Attempts);
        Assert.NotNull(savedJob.ProcessedAt);
        Assert.Null(savedJob.PublisherLockedUntil);
        Assert.Null(savedJob.LastError);
        Assert.Equal("Succeeded", delivery.Result);
        Assert.Equal(job.NotificationJobId, delivery.NotificationJobId);
    }

    [Fact]
    public async Task ProcessAsync_EmailSenderThrows_RecordsFailureAndMovesJobToFailedAtMaxAttempts()
    {
        await using SqliteTestDatabase database = await SqliteTestDatabase.CreateAsync();
        NotificationJob job = SeedNotificationJob(database.DbContext, attempts: 1);
        await database.DbContext.SaveChangesAsync();
        var processor = new NotificationJobProcessor(
            database.DbContext,
            new FakeEmailNotificationSender(new InvalidOperationException("smtp unavailable")));

        NotificationJobProcessResult result = await processor.ProcessAsync(job.NotificationJobId, maxAttempts: 2);

        NotificationJob savedJob = await database.DbContext.NotificationJobs.SingleAsync();
        NotificationDelivery delivery = await database.DbContext.NotificationDeliveries.SingleAsync();
        Assert.Equal(NotificationJobProcessStatus.Failed, result.Status);
        Assert.Equal("smtp unavailable", result.Error);
        Assert.Equal(4, savedJob.JobStatusId);
        Assert.Equal(2, savedJob.Attempts);
        Assert.NotNull(savedJob.ProcessedAt);
        Assert.Equal("smtp unavailable", savedJob.LastError);
        Assert.Equal("Failed", delivery.Result);
    }

    private static NotificationJob SeedNotificationJob(
        AppDbContext dbContext,
        int jobStatusId = 1,
        int attempts = 0,
        string? payload = null)
    {
        Guid eventId = Guid.NewGuid();
        Guid recipientUserId = Guid.NewGuid();
        var organizer = new User
        {
            UserId = Guid.NewGuid(),
            Email = "teacher@example.com",
            PasswordHash = "hash",
            DisplayName = "Teacher",
            RoleId = 2
        };
        var recipient = new User
        {
            UserId = recipientUserId,
            Email = "student@example.com",
            PasswordHash = "hash",
            DisplayName = "Student",
            RoleId = 1
        };
        var ev = new Event
        {
            EventId = eventId,
            OrganizerId = organizer.UserId,
            EventStatusId = 2,
            Title = "Published",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(1),
            Capacity = 5
        };
        var job = new NotificationJob
        {
            NotificationJobId = Guid.NewGuid(),
            EventId = eventId,
            RecipientUserId = recipientUserId,
            JobStatusId = jobStatusId,
            Attempts = attempts,
            Payload = payload ?? $$"""{"type":"RegistrationConfirmed","event_id":"{{eventId}}","student_id":"{{recipientUserId}}"}""",
            Title = "Registration Confirmed",
            Message = "Your seat is confirmed.",
            Channel = "Email"
        };

        dbContext.Users.AddRange(organizer, recipient);
        dbContext.Events.Add(ev);
        dbContext.NotificationJobs.Add(job);
        return job;
    }

    private sealed class FakeEmailNotificationSender(Exception? exception = null) : IEmailNotificationSender
    {
        public List<EmailNotification> Sent { get; } = [];

        public Task SendAsync(EmailNotification notification, CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Sent.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private SqliteTestDatabase(SqliteConnection connection, AppDbContext dbContext)
        {
            this.connection = connection;
            DbContext = dbContext;
        }

        public AppDbContext DbContext { get; }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new AppDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new SqliteTestDatabase(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
