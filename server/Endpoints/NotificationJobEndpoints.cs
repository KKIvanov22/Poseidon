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
