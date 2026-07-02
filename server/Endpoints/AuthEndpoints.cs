using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Poseidon.Server.Auth;
using Poseidon.Server.Data;
using Poseidon.Server.Data.Entities;
using Poseidon.Server.RateLimiting;

namespace Poseidon.Server.Endpoints;

public static class AuthEndpoints
{
    private const int StudentRoleId = 1;

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/auth")
            .WithTags("Auth")
            .RequireRateLimiting(RateLimitPolicies.Api);

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .WithSummary("Register a student")
            .WithDescription("Creates a student account and returns a JWT access token for the new user.")
            .Accepts<RegisterRequest>("application/json")
            .Produces<AuthResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .WithSummary("Log in")
            .WithDescription("Authenticates a user with email and password and returns a JWT access token.")
            .Accepts<LoginRequest>("application/json")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Log out")
            .WithDescription("Requires a bearer token and tells the client to discard it. JWT logout is client-side only.")
            .Produces<LogoutResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static async Task<Results<Created<AuthResponse>, ProblemHttpResult>> RegisterAsync(
        RegisterRequest request,
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return ValidationProblem("Display name is required.");
        }

        if (!AuthValidation.IsValidEmail(request.Email))
        {
            return ValidationProblem("Enter a valid email address.");
        }

        if (!AuthValidation.IsValidPassword(request.Password))
        {
            return ValidationProblem(AuthValidation.PasswordRequirement);
        }

        string email = NormalizeEmail(request.Email);

        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName.Trim(),
            RoleId = StudentRoleId
        };

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return ConflictProblem("A user with this email already exists.");
        }

        user.Role = await dbContext.UserRoles.FindAsync(user.RoleId);
        return TypedResults.Created($"/users/{user.UserId}", CreateAuthResponse(user, jwtOptions.Value));
    }

    private static async Task<Results<Ok<AuthResponse>, UnauthorizedHttpResult, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        AppDbContext dbContext,
        IOptions<JwtOptions> jwtOptions)
    {
        if (!AuthValidation.IsValidEmail(request.Email))
        {
            return ValidationProblem("Enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return ValidationProblem("Password is required.");
        }

        string email = NormalizeEmail(request.Email);

        User? user = await dbContext.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Email.ToLower() == email);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(CreateAuthResponse(user, jwtOptions.Value));
    }

    private static Ok<LogoutResponse> Logout()
    {
        return TypedResults.Ok(new LogoutResponse("Logged out. Discard the bearer token on the client."));
    }

    private static ProblemHttpResult ValidationProblem(string detail)
    {
        return TypedResults.Problem(
            title: "Invalid request.",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static ProblemHttpResult ConflictProblem(string detail)
    {
        return TypedResults.Problem(
            title: "Conflict.",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
    }

    private static AuthResponse CreateAuthResponse(User user, JwtOptions options)
    {
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpirationMinutes);
        string role = user.Role?.RoleName ?? "Student";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(JwtClaimNames.Role, role)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthResponse(
            user.UserId,
            user.Email,
            user.DisplayName,
            role,
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}

public sealed record RegisterRequest(string Email, string Password, string DisplayName);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string AccessToken,
    DateTimeOffset ExpiresAt);

public sealed record LogoutResponse(string Message);

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "Poseidon";
    public string Audience { get; init; } = "Poseidon";
    public string SigningKey { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 60;
}

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        JwtOptions jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured.");
        }

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = JwtClaimNames.Role,
                    NameClaimType = JwtRegisteredClaimNames.Name
                };
            });

        services.AddAuthorization();

        return services;
    }
}
