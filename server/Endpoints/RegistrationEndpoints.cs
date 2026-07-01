using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
using Poseidon.Server.RateLimiting;
using Poseidon.Server.Services;

namespace Poseidon.Server.Endpoints;

public static class RegistrationEndpoints
{
    public static RouteGroupBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/registrations")
            .WithTags("Registrations")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .RequireAuthorization();

        group.MapGet("/me", GetMyRegistrationsAsync)
            .RequireRole(UserRoles.Student)
            .WithName("GetMyRegistrations")
            .WithSummary("List the current student's active and cancelled registrations")
            .Produces<List<MyRegistrationResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);

        group.MapDelete("/{registrationId:guid}", CancelAsync)
            .RequireRole(UserRoles.Student)
            .WithName("CancelMyRegistration")
            .WithSummary("Cancel the current student's registration")
            .Produces<CancelRegistrationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<List<MyRegistrationResponse>>, BadRequest<ProblemHttpResult>>> GetMyRegistrationsAsync(
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        if (!TryGetUserId(user, out Guid studentId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        var registrations = await dbContext.Registrations
            .AsNoTracking()
            .Include(registration => registration.Event)
            .Where(registration => registration.StudentId == studentId)
            .OrderByDescending(registration => registration.RegisteredAt)
            .Select(registration => new
            {
                registration.RegistrationId,
                registration.EventId,
                EventTitle = registration.Event != null ? registration.Event.Title : string.Empty,
                registration.RegistrationStatusId,
                registration.RegisteredAt,
                registration.CancelledAt
            })
            .ToListAsync();

        var response = new List<MyRegistrationResponse>();
        foreach (var registration in registrations)
        {
            int? waitlistPosition = null;
            if (registration.RegistrationStatusId == 2 && registration.CancelledAt is null)
            {
                waitlistPosition = await dbContext.Registrations.CountAsync(other =>
                    other.EventId == registration.EventId &&
                    other.RegistrationStatusId == 2 &&
                    other.CancelledAt == null &&
                    other.RegisteredAt <= registration.RegisteredAt);
            }

            response.Add(new MyRegistrationResponse(
                registration.RegistrationId,
                registration.EventId,
                registration.EventTitle,
                MapRegistrationStatus(registration.RegistrationStatusId),
                waitlistPosition,
                registration.RegisteredAt,
                registration.CancelledAt));
        }

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<CancelRegistrationResponse>, BadRequest<ProblemHttpResult>, NotFound>> CancelAsync(
        Guid registrationId,
        ClaimsPrincipal user,
        AppDbContext dbContext,
        IRegistrationOrchestrator registrationOrchestrator)
    {
        if (!TryGetUserId(user, out Guid studentId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        var registration = await dbContext.Registrations
            .AsNoTracking()
            .Where(registration =>
                registration.RegistrationId == registrationId &&
                registration.StudentId == studentId &&
                registration.CancelledAt == null)
            .Select(registration => new
            {
                registration.EventId,
                registration.RegistrationStatusId
            })
            .SingleOrDefaultAsync();

        if (registration is null)
        {
            return TypedResults.NotFound();
        }

        CancelRegistrationTransactionResult result =
            await registrationOrchestrator.CancelRegistrationTransactionAsync(registration.EventId, studentId);

        if (!result.WasCancelled)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new CancelRegistrationResponse(
            result.CancelledRegistrationId!.Value,
            registration.EventId,
            MapRegistrationStatus(result.PreviousStatusId ?? registration.RegistrationStatusId),
            result.PromotedRegistrationId,
            result.PromotedStudentId));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string MapRegistrationStatus(int statusId) => statusId switch
    {
        1 => "Confirmed",
        2 => "Waitlisted",
        3 => "Cancelled",
        _ => "Unknown"
    };
}

public sealed record MyRegistrationResponse(
    Guid RegistrationId,
    Guid EventId,
    string EventTitle,
    string RegistrationStatus,
    int? WaitlistPosition,
    DateTimeOffset RegisteredAt,
    DateTimeOffset? CancelledAt);

public sealed record CancelRegistrationResponse(
    Guid RegistrationId,
    Guid EventId,
    string PreviousRegistrationStatus,
    Guid? PromotedRegistrationId,
    Guid? PromotedStudentId);
