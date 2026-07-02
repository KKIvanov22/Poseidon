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
LoadDotEnvIntoEnvironment(builder.Environment.ContentRootPath);
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddInMemoryCollection(BuildEnvironmentConfigurationAliases());

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
app.MapPushDeviceTokenEndpoints();

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

static void LoadDotEnvIntoEnvironment(string contentRootPath)
{
    string envPath = Path.Combine(contentRootPath, ".env");
    if (!File.Exists(envPath))
    {
        return;
    }

    foreach (string line in File.ReadLines(envPath))
    {
        string trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            continue;
        }

        int separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        string key = trimmed[..separatorIndex].Trim();
        string value = trimmed[(separatorIndex + 1)..].Trim();
        value = UnquoteEnvValue(value);

        if (string.IsNullOrWhiteSpace(key) ||
            Environment.GetEnvironmentVariable(key) is not null)
        {
            continue;
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}

static Dictionary<string, string?> BuildEnvironmentConfigurationAliases()
{
    var aliases = new Dictionary<string, string?>();

    AddAlias(aliases, "RabbitMq:HostName", "RABBITMQ_HOST");
    AddAlias(aliases, "RabbitMq:Port", "RABBITMQ_PORT");
    AddAlias(aliases, "RabbitMq:UserName", "RABBITMQ_USERNAME", "RABBITMQ_USER");
    AddAlias(aliases, "RabbitMq:Password", "RABBITMQ_PASSWORD");
    AddAlias(aliases, "RabbitMq:VirtualHost", "RABBITMQ_VHOST", "RABBITMQ_VIRTUAL_HOST");
    AddAlias(aliases, "RabbitMq:RequireTls", "RABBITMQ_REQUIRE_TLS", "RABBITMQ_TLS");
    AddAlias(aliases, "RabbitMq:TlsServerName", "RABBITMQ_TLS_SERVER_NAME");
    AddAlias(aliases, "Firebase:CloudMessaging:Enabled", "FIREBASE_CLOUD_MESSAGING_ENABLED");
    AddAlias(aliases, "Firebase:CloudMessaging:ProjectId", "FIREBASE_CLOUD_MESSAGING_PROJECT_ID");
    AddAlias(aliases, "Firebase:CloudMessaging:CredentialPath", "FIREBASE_CLOUD_MESSAGING_CREDENTIAL_PATH");
    AddAlias(aliases, "Firebase:CloudMessaging:CredentialJson", "FIREBASE_CLOUD_MESSAGING_CREDENTIAL_JSON");
    AddAlias(aliases, "Firebase:CloudMessaging:PrivateKeyId", "FIREBASE_CLOUD_MESSAGING_PRIVATE_KEY_ID");
    AddAlias(aliases, "Firebase:CloudMessaging:PrivateKey", "FIREBASE_CLOUD_MESSAGING_PRIVATE_KEY");
    AddAlias(aliases, "Firebase:CloudMessaging:ClientEmail", "FIREBASE_CLOUD_MESSAGING_CLIENT_EMAIL");
    AddAlias(aliases, "Firebase:CloudMessaging:ClientId", "FIREBASE_CLOUD_MESSAGING_CLIENT_ID");
    AddAlias(aliases, "Firebase:CloudMessaging:ClientX509CertUrl", "FIREBASE_CLOUD_MESSAGING_CLIENT_X509_CERT_URL");

    string? rabbitMqPort = Environment.GetEnvironmentVariable("RABBITMQ_PORT");
    if (!aliases.ContainsKey("RabbitMq:RequireTls") &&
        string.Equals(rabbitMqPort, "5671", StringComparison.Ordinal))
    {
        aliases["RabbitMq:RequireTls"] = "true";
    }

    if (Environment.GetEnvironmentVariable("ConnectionStrings__Default") is null)
    {
        string? connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = BuildPostgresConnectionString();
        }

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            aliases["ConnectionStrings:Default"] = connectionString;
        }
    }

    return aliases;
}

static void AddAlias(
    IDictionary<string, string?> aliases,
    string configurationKey,
    params string[] environmentKeys)
{
    foreach (string environmentKey in environmentKeys)
    {
        string? value = Environment.GetEnvironmentVariable(environmentKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            aliases[configurationKey] = value;
            return;
        }
    }
}

static string? BuildPostgresConnectionString()
{
    string host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    string port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    string database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "poseidon";
    string? username = Environment.GetEnvironmentVariable("POSTGRES_APP_USER");
    string? password = Environment.GetEnvironmentVariable("POSTGRES_APP_PASSWORD");

    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        return null;
    }

    return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

static string UnquoteEnvValue(string value)
{
    if (value.Length >= 2 &&
        ((value[0] == '"' && value[^1] == '"') ||
        (value[0] == '\'' && value[^1] == '\'')))
    {
        return value[1..^1];
    }

    return value;
}

public partial class Program;
