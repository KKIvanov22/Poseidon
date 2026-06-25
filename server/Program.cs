using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Poseidon.Server.Data;
using Poseidon.Server.Endpoints;
using Poseidon.Server.Services;

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
builder.Services.AddScoped<IRegistrationOrchestrator, RegistrationOrchestrator>();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddCors(options =>
{
    string[] allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:3000"];

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
.Produces(StatusCodes.Status200OK);

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapEventEndpoints();

app.Run();
