using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Poseidon.Server.Data;
using Poseidon.Server.Endpoints;

namespace Poseidon.Server.Tests.Integration;

public sealed class AuthEndpointsTests
{
    [Fact]
    public async Task Register_ReturnsCreatedStudentAuthResponse()
    {
        await using var factory = new PoseidonWebApplicationFactory();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/auth/register",
            new RegisterRequest("  NEW.STUDENT@EXAMPLE.COM  ", "Password123!", "  New Student  "));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AuthResponse? body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.Equal("new.student@example.com", body.Email);
        Assert.Equal("New Student", body.DisplayName);
        Assert.Equal("Student", body.Role);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    private sealed class PoseidonWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "Poseidon.Tests",
                    ["Jwt:Audience"] = "Poseidon.Tests",
                    ["Jwt:SigningKey"] = "poseidon-tests-signing-key-with-enough-length",
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["RabbitMq:Enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                ServiceDescriptor? descriptor = services.SingleOrDefault(
                    service => service.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase($"poseidon-integration-{Guid.NewGuid()}"));
            });
        }
    }
}
