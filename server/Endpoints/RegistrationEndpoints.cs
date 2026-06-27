using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Services;

namespace Poseidon.Server.Endpoints;

public static class RegistrationEndpoints
{
    public static RouteGroupBuilder MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/registrations")
            .WithTags("Registrations")
            .RequireAuthorization();

        // BE-19: Get all registrations belonging to the authenticated user
        group.MapGet("/me", GetMyRegistrationsAsync)
            .WithName("GetMyRegistrations")
            .WithSummary("Retrieve all registrations for the logged-in student.");

        // BE-20: Get confirmed registrations for the authenticated user
        group.MapGet("/confirmed", GetMyConfirmedRegistrationsAsync)
            .WithName("GetMyConfirmedRegistrations")
            .WithSummary("Retrieve only confirmed event seats for the logged-in student.");

        // BE-15 & BE-17: Process a registration check
        group.MapPost("/event/{eventId:guid}", RegisterForEventAsync)
            .WithName("RegisterForEvent")
            .WithSummary("Register or waitlist a student for an active event.");

        // BE-16: Self-service cancellation route handler
        group.MapDelete("/event/{eventId:guid}", CancelRegistrationAsync)
            .WithName("CancelRegistration")
            .WithSummary("Cancel an active registration and trigger background queue promotion.");

        return group;
    }

    private static async Task<IResult> GetMyRegistrationsAsync(
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var userId = GetUserId(user);
        
        // Query registrations matching historical schema records directly from context sql views
        var registrations = await dbContext.Database
            .SqlQuery<RegistrationResponseDto>($$"""
                SELECT r.registration_id as RegistrationId, r.event_id as EventId, e.title as EventTitle, r.registration_status_id as StatusId, r.registered_at as RegisteredAt
                FROM public.registrations r
                JOIN public.events e ON r.event_id = e.event_id
                WHERE r.student_id = {0} AND r.cancelled_at IS NULL
                """)
            .ToListAsync();

        return TypedResults.Ok(registrations);
    }

    private static async Task<IResult> GetMyConfirmedRegistrationsAsync(
        ClaimsPrincipal user,
        AppDbContext dbContext)
    {
        var userId = GetUserId(user);

        var confirmed = await dbContext.Database
            .SqlQuery<RegistrationResponseDto>($$"""
                SELECT r.registration_id as RegistrationId, r.event_id as EventId, e.title as EventTitle, r.registration_status_id as StatusId, r.registered_at as RegisteredAt
                FROM public.registrations r
                JOIN public.events e ON r.event_id = e.event_id
                WHERE r.student_id = {0} AND r.registration_status_id = 1 AND r.cancelled_at IS NULL
                """)
            .ToListAsync();

        return TypedResults.Ok(confirmed);
    }

    private static async Task<IResult> RegisterForEventAsync(
        Guid eventId,
        ClaimsPrincipal user,
        IRegistrationOrchestrator orchestrator)
    {
        var userId = GetUserId(user);
        
        // Pass payload straight into transaction loop blocks
        var (registrationId, statusId) = await orchestrator.RegisterStudentTransactionAsync(eventId, userId);

        var resultDescription = statusId == 1 
            ? "Registration successfully confirmed. Seat secured!" 
            : "Event capacity reached. You have been placed on the waitlist queue.";

        return TypedResults.Ok(new { RegistrationId = registrationId, StatusId = statusId, Message = resultDescription });
    }

    private static async Task<IResult> CancelRegistrationAsync(
        Guid eventId,
        ClaimsPrincipal user,
        IRegistrationOrchestrator orchestrator)
    {
        var userId = GetUserId(user);
        
        // Execute safe extraction and trigger atomic sequence processing logic
        Guid? promotedUser = await orchestrator.CancelRegistrationTransactionAsync(eventId, userId);

        return TypedResults.Ok(new 
        { 
            Message = "Registration canceled successfully.",
            NextWaitlistUserPromoted = promotedUser.HasValue,
            PromotedUserId = promotedUser
        });
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var nameIdentifier = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? throw new InvalidOperationException("User structural identification context missing.");
        return Guid.Parse(nameIdentifier);
    }
}

public sealed record RegistrationResponseDto(Guid RegistrationId, Guid EventId, string EventTitle, int StatusId, DateTime RegisteredAt);