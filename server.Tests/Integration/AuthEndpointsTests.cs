using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Poseidon.Server.Data;
using Poseidon.Server.Endpoints;
using Xunit;

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

        string responseText = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected Created but got {response.StatusCode}: {responseText}");
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

            builder.ConfigureTestServices(services =>
            {
                for (int i = services.Count - 1; i >= 0; i--)
                {
                    ServiceDescriptor descriptor = services[i];
                    string serviceType = descriptor.ServiceType.FullName ?? string.Empty;
                    string implementationType = descriptor.ImplementationType?.FullName ?? string.Empty;
                    if (serviceType.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
                        serviceType.Contains("Npgsql", StringComparison.Ordinal) ||
                        implementationType.Contains("EntityFrameworkCore", StringComparison.Ordinal) ||
                        implementationType.Contains("Npgsql", StringComparison.Ordinal))
                    {
                        services.RemoveAt(i);
                    }
                }

                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<DbContextOptions>();

                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase($"poseidon-integration-{Guid.NewGuid()}"));
            });
        }
    }
}
