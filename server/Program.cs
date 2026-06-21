using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;
using Poseidon.Server.Endpoints;
using Poseidon.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IRegistrationOrchestrator, RegistrationOrchestrator>();
builder.Services.AddJwtAuthentication(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/ping", () => Results.Ok(new
{
    status = "ok",
    message = "pong",
    checkedAt = DateTimeOffset.UtcNow
}))
.WithName("Ping")
.WithTags("Health");

app.MapAuthEndpoints();

app.Run();
