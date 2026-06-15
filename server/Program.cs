using Microsoft.EntityFrameworkCore;
using Poseidon.Server.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase(builder.Configuration.GetConnectionString("Default") ?? "Poseidon"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health/ping", () => Results.Ok(new
{
    status = "ok",
    message = "pong",
    checkedAt = DateTimeOffset.UtcNow
}))
.WithName("Ping")
.WithTags("Health");

app.Run();
