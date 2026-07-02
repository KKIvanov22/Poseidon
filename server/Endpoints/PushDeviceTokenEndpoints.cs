using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.RateLimiting;

namespace Poseidon.Server.Endpoints;

public static class PushDeviceTokenEndpoints
{
    public static RouteGroupBuilder MapPushDeviceTokenEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/push-tokens")
            .WithTags("Push Tokens")
            .RequireRateLimiting(RateLimitPolicies.Api)
            .RequireAuthorization();

        group.MapPost("/", RegisterAsync)
            .WithName("RegisterPushToken")
            .WithSummary("Register the current user's Android FCM token")
            .Accepts<RegisterPushTokenRequest>("application/json")
            .Produces<PushTokenResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapDelete("/", RevokeAsync)
            .WithName("RevokePushToken")
            .WithSummary("Revoke an Android FCM token for the current user")
            .Accepts<RevokePushTokenRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Ok<PushTokenResponse>, BadRequest<ProblemHttpResult>>> RegisterAsync(
        [FromBody] RegisterPushTokenRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out Guid userId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        string tokenValue = request.Token.Trim();
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return TypedResults.BadRequest(TypedResults.Problem("A push token is required."));
        }

        string platform = NormalizePlatform(request.Platform);
        if (platform != "Android")
        {
            return TypedResults.BadRequest(TypedResults.Problem("Only Android push tokens are supported."));
        }

        PushDeviceToken? token = await dbContext.PushDeviceTokens
            .SingleOrDefaultAsync(item => item.Token == tokenValue, cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (token is null)
        {
            token = new PushDeviceToken
            {
                UserId = userId,
                Token = tokenValue,
                Platform = platform,
                DeviceId = NormalizeDeviceId(request.DeviceId),
                CreatedAt = now,
                UpdatedAt = now,
                LastSeenAt = now
            };
            dbContext.PushDeviceTokens.Add(token);
        }
        else
        {
            token.UserId = userId;
            token.Platform = platform;
            token.DeviceId = NormalizeDeviceId(request.DeviceId);
            token.UpdatedAt = now;
            token.LastSeenAt = now;
            token.RevokedAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new PushTokenResponse(token.PushDeviceTokenId, token.Platform, token.LastSeenAt));
    }

    private static async Task<Results<NoContent, BadRequest<ProblemHttpResult>>> RevokeAsync(
        [FromBody] RevokePushTokenRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(principal, out Guid userId))
        {
            return TypedResults.BadRequest(TypedResults.Problem("Invalid user claim data."));
        }

        string tokenValue = request.Token.Trim();
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return TypedResults.BadRequest(TypedResults.Problem("A push token is required."));
        }

        PushDeviceToken? token = await dbContext.PushDeviceTokens
            .SingleOrDefaultAsync(item => item.Token == tokenValue && item.UserId == userId, cancellationToken);

        if (token is not null)
        {
            token.RevokedAt = DateTimeOffset.UtcNow;
            token.UpdatedAt = token.RevokedAt.Value;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string NormalizePlatform(string? platform) =>
        string.IsNullOrWhiteSpace(platform) ? "Android" : platform.Trim();

    private static string? NormalizeDeviceId(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim();
}

public sealed record RegisterPushTokenRequest(string Token, string? Platform, string? DeviceId);

public sealed record RevokePushTokenRequest(string Token);

public sealed record PushTokenResponse(Guid PushDeviceTokenId, string Platform, DateTimeOffset LastSeenAt);
