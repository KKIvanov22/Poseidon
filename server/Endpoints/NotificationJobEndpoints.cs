using Microsoft.AspNetCore.Http.HttpResults;
using Poseidon.Server.Auth;
using Poseidon.Server.Services.Notifications;

namespace Poseidon.Server.Endpoints;

public static class NotificationJobEndpoints
{
    public static RouteGroupBuilder MapNotificationJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/notifications/jobs")
            .WithTags("Notification Jobs")
            .RequireAuthorization();

        group.MapGet("/pending", ReadPendingAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("ReadPendingNotificationJobs")
            .WithSummary("Read due pending notification jobs")
            .Produces<List<PendingNotificationJobResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapPost("/{id:guid}/complete", MarkCompletedAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("MarkNotificationJobCompleted")
            .WithSummary("Mark a notification job completed")
            .Produces<CompletedNotificationJobResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/retry", RetryAsync)
            .RequireRole(UserRoles.Admin)
            .WithName("RetryNotificationJob")
            .WithSummary("Retry a failed notification job")
            .Produces<RetriedNotificationJobResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Ok<List<PendingNotificationJobResponse>>> ReadPendingAsync(
        int? limit,
        INotificationJobReader jobReader,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingNotificationJob> jobs = await jobReader.ReadPendingAsync(
            limit ?? 25,
            cancellationToken: cancellationToken);

        return TypedResults.Ok(jobs.Select(MapToResponse).ToList());
    }

    private static async Task<Results<Ok<CompletedNotificationJobResponse>, NotFound, Conflict<ProblemHttpResult>>> MarkCompletedAsync(
        Guid id,
        INotificationJobCompletionService completionService,
        CancellationToken cancellationToken)
    {
        NotificationJobCompletionResult result = await completionService.MarkCompletedAsync(id, cancellationToken);

        return result.Status switch
        {
            NotificationJobCompletionStatus.Completed => TypedResults.Ok(MapToCompletedResponse(result.Job!)),
            NotificationJobCompletionStatus.NotFound => TypedResults.NotFound(),
            NotificationJobCompletionStatus.CannotComplete => TypedResults.Conflict(TypedResults.Problem(result.Problem)),
            _ => TypedResults.Conflict(TypedResults.Problem("Unable to complete this notification job."))
        };
    }

    private static async Task<Results<Ok<RetriedNotificationJobResponse>, NotFound, Conflict<ProblemHttpResult>>> RetryAsync(
        Guid id,
        INotificationJobRetryService retryService,
        CancellationToken cancellationToken)
    {
        NotificationJobRetryResult result = await retryService.RetryAsync(id, cancellationToken);

        return result.Status switch
        {
            NotificationJobRetryStatus.Retried => TypedResults.Ok(MapToRetriedResponse(result.Job!)),
            NotificationJobRetryStatus.NotFound => TypedResults.NotFound(),
            NotificationJobRetryStatus.CannotRetry => TypedResults.Conflict(TypedResults.Problem(result.Problem)),
            _ => TypedResults.Conflict(TypedResults.Problem("Unable to retry this notification job."))
        };
    }

    private static PendingNotificationJobResponse MapToResponse(PendingNotificationJob job) =>
        new(
            job.NotificationJobId,
            job.EventId,
            job.RecipientUserId,
            job.Channel,
            job.Title,
            job.Message,
            job.PayloadJson,
            job.Attempts,
            job.AvailableAt,
            job.CreatedAt);

    private static CompletedNotificationJobResponse MapToCompletedResponse(Data.Entities.NotificationJob job) =>
        new(
            job.NotificationJobId,
            job.JobStatusId,
            "Succeeded",
            job.Attempts,
            job.ProcessedAt);

    private static RetriedNotificationJobResponse MapToRetriedResponse(Data.Entities.NotificationJob job) =>
        new(
            job.NotificationJobId,
            job.JobStatusId,
            "Pending",
            job.Attempts,
            job.AvailableAt);
}

public sealed record PendingNotificationJobResponse(
    Guid NotificationJobId,
    Guid EventId,
    Guid RecipientUserId,
    string Channel,
    string Title,
    string Message,
    string PayloadJson,
    int Attempts,
    DateTimeOffset AvailableAt,
    DateTimeOffset CreatedAt);

public sealed record CompletedNotificationJobResponse(
    Guid NotificationJobId,
    int JobStatusId,
    string JobStatus,
    int Attempts,
    DateTimeOffset? ProcessedAt);

public sealed record RetriedNotificationJobResponse(
    Guid NotificationJobId,
    int JobStatusId,
    string JobStatus,
    int Attempts,
    DateTimeOffset AvailableAt);
