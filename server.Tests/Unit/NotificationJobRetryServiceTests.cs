using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.Services.Notifications;
using Xunit;

namespace Poseidon.Server.Tests.Unit;

public sealed class NotificationJobRetryServiceTests
{
    [Fact]
    public async Task RetryAsync_MissingJob_ReturnsNotFound()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        var service = new NotificationJobRetryService(dbContext);

        NotificationJobRetryResult result = await service.RetryAsync(Guid.NewGuid());

        Assert.Equal(NotificationJobRetryStatus.NotFound, result.Status);
        Assert.Null(result.Job);
        Assert.Null(result.Problem);
    }

    [Fact]
    public async Task RetryAsync_PendingJob_ReturnsCannotRetryAndKeepsJobPending()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        NotificationJob job = SeedNotificationJob(dbContext, jobStatusId: 1, attempts: 2, lastError: "smtp down");
        await dbContext.SaveChangesAsync();
        var service = new NotificationJobRetryService(dbContext);

        NotificationJobRetryResult result = await service.RetryAsync(job.NotificationJobId);

        NotificationJob savedJob = await dbContext.NotificationJobs.SingleAsync();
        Assert.Equal(NotificationJobRetryStatus.CannotRetry, result.Status);
        Assert.Equal("Only failed notification jobs can be retried.", result.Problem);
        Assert.Same(savedJob, result.Job);
        Assert.Equal(1, savedJob.JobStatusId);
        Assert.Equal(2, savedJob.Attempts);
        Assert.Equal("smtp down", savedJob.LastError);
    }

    [Fact]
    public async Task RetryAsync_SucceededJob_ReturnsCannotRetryAndKeepsProcessedAt()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        DateTimeOffset processedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        NotificationJob job = SeedNotificationJob(
            dbContext,
            jobStatusId: 3,
            attempts: 1,
            processedAt: processedAt);
        await dbContext.SaveChangesAsync();
        var service = new NotificationJobRetryService(dbContext);

        NotificationJobRetryResult result = await service.RetryAsync(job.NotificationJobId);

        NotificationJob savedJob = await dbContext.NotificationJobs.SingleAsync();
        Assert.Equal(NotificationJobRetryStatus.CannotRetry, result.Status);
        Assert.Equal(3, savedJob.JobStatusId);
        Assert.Equal(processedAt, savedJob.ProcessedAt);
    }

    [Fact]
    public async Task RetryAsync_FailedJob_ResetsStateAndReturnsRetried()
    {
        await using AppDbContext dbContext = CreateInMemoryDbContext();
        DateTimeOffset oldAvailableAt = DateTimeOffset.UtcNow.AddDays(-1);
        NotificationJob job = SeedNotificationJob(
            dbContext,
            jobStatusId: 4,
            attempts: 5,
            availableAt: oldAvailableAt,
            publisherLockedUntil: DateTimeOffset.UtcNow.AddMinutes(5),
            publishedAt: DateTimeOffset.UtcNow.AddMinutes(-5),
            processedAt: DateTimeOffset.UtcNow.AddMinutes(-4),
            lastError: "delivery failed");
        await dbContext.SaveChangesAsync();
        var service = new NotificationJobRetryService(dbContext);

        NotificationJobRetryResult result = await service.RetryAsync(job.NotificationJobId);

        NotificationJob savedJob = await dbContext.NotificationJobs.SingleAsync();
        Assert.Equal(NotificationJobRetryStatus.Retried, result.Status);
        Assert.Same(savedJob, result.Job);
        Assert.Equal(1, savedJob.JobStatusId);
        Assert.Equal(0, savedJob.Attempts);
        Assert.True(savedJob.AvailableAt >= oldAvailableAt);
        Assert.Null(savedJob.PublisherLockedUntil);
        Assert.Null(savedJob.PublishedAt);
        Assert.Null(savedJob.ProcessedAt);
        Assert.Null(savedJob.LastError);
    }

    [Fact]
    public async Task RetryAsync_FailedJob_PersistsResetForNewContext()
    {
        string databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        Guid jobId;

        await using (AppDbContext seedContext = CreateInMemoryDbContext(databaseName, databaseRoot))
        {
            NotificationJob job = SeedNotificationJob(seedContext, jobStatusId: 4, attempts: 3, lastError: "timeout");
            jobId = job.NotificationJobId;
            await seedContext.SaveChangesAsync();
            var service = new NotificationJobRetryService(seedContext);

            await service.RetryAsync(jobId);
        }

        await using AppDbContext assertionContext = CreateInMemoryDbContext(databaseName, databaseRoot);
        NotificationJob savedJob = await assertionContext.NotificationJobs.SingleAsync(job => job.NotificationJobId == jobId);
        Assert.Equal(1, savedJob.JobStatusId);
        Assert.Equal(0, savedJob.Attempts);
        Assert.Null(savedJob.LastError);
    }

    private static AppDbContext CreateInMemoryDbContext(string? databaseName = null, InMemoryDatabaseRoot? databaseRoot = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (databaseRoot is null)
        {
            builder.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        }
        else
        {
            builder.UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), databaseRoot);
        }

        return new AppDbContext(builder.Options);
    }

    private static NotificationJob SeedNotificationJob(
        AppDbContext dbContext,
        int jobStatusId,
        int attempts = 0,
        DateTimeOffset? availableAt = null,
        DateTimeOffset? publisherLockedUntil = null,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? processedAt = null,
        string? lastError = null)
    {
        var job = new NotificationJob
        {
            NotificationJobId = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            RecipientUserId = Guid.NewGuid(),
            JobStatusId = jobStatusId,
            Attempts = attempts,
            AvailableAt = availableAt ?? DateTimeOffset.UtcNow,
            PublisherLockedUntil = publisherLockedUntil,
            PublishedAt = publishedAt,
            ProcessedAt = processedAt,
            LastError = lastError,
            Payload = """{"type":"RegistrationConfirmed"}""",
            Title = "Registration Confirmed",
            Message = "Your seat is confirmed.",
            Channel = "Email"
        };

        dbContext.NotificationJobs.Add(job);
        return job;
    }
}
