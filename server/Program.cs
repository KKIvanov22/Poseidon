using System.Security.Claims;
using System.Net.Mail;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Poseidon.Server.Data;
using Poseidon.Server.Endpoints;
using Poseidon.Server.RateLimiting;
using Poseidon.Server.Services;
using Poseidon.Server.Services.Notifications;

var builder = WebApplication.CreateBuilder(args);

const string ClientCorsPolicy = "ClientCorsPolicy";

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Poseidon Server API",
        Version = "v1",
        Description = "API documentation for Poseidon server endpoints."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT bearer token returned by the login or register endpoint."
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            []
        }
    });
});
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IRegistrationOrchestrator, RegistrationOrchestrator>();
builder.Services
    .AddFluentEmail(
        builder.Configuration["Smtp:FromEmail"] ?? "no-reply@poseidon.com",
        builder.Configuration["Smtp:FromName"] ?? "Poseidon Events System")
    .AddSmtpSender(() => new SmtpClient
    {
        Host = builder.Configuration["Smtp:Host"] ?? "localhost",
        Port = builder.Configuration.GetValue("Smtp:Port", 1025),
        EnableSsl = builder.Configuration.GetValue("Smtp:EnableSsl", false)
    });
builder.Services.AddNotificationMessaging(builder.Configuration, builder.Environment);
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    ApiRateLimitOptions rateLimitOptions = builder.Configuration
        .GetSection(ApiRateLimitOptions.SectionName)
        .Get<ApiRateLimitOptions>() ?? new ApiRateLimitOptions();

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await TypedResults.Problem(
            title: "Too many requests.",
            detail: "The endpoint rate limit has been exceeded. Please retry later.",
            statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(context.HttpContext);
    };

    options.AddPolicy(RateLimitPolicies.Api, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, rateLimitOptions.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, rateLimitOptions.WindowSeconds)),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));
});

builder.Services.AddCors(options =>
{
    string[] allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:3000"];
    allowedOrigins = allowedOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .ToArray();

    options.AddPolicy(ClientCorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Poseidon Server API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors(ClientCorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health/ping", () => Results.Ok(new
{
    status = "ok",
    message = "pong",
    checkedAt = DateTimeOffset.UtcNow
}))
.WithName("Ping")
.WithTags("Health")
.WithSummary("Ping the API")
.WithDescription("Checks that the API process is running and returns the current UTC check time.")
.RequireRateLimiting(RateLimitPolicies.Api)
.Produces(StatusCodes.Status200OK);

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapEventEndpoints();
app.MapRegistrationEndpoints();
app.MapWaitlistEndpoints(); // Maps the new waitlist retrieval route
app.MapNotificationJobEndpoints();

app.Run();

static string GetRateLimitPartitionKey(HttpContext httpContext)
{
    string? userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    return $"ip:{httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}

public partial class Program;
