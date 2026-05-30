using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DiscWeave.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "The health endpoint returns the service status")]
    public async Task The_health_endpoint_returns_the_service_status()
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        const string connectionStringVariableName = "ConnectionStrings__DiscWeave";
        string? previousConnectionString = Environment.GetEnvironmentVariable(connectionStringVariableName);
        Environment.SetEnvironmentVariable(connectionStringVariableName, "Host=localhost;Database=discweave;Username=discweave");

        try
        {
            HttpClient client = _factory.CreateClient();

            using HttpResponseMessage response = await client.GetAsync("/health", timeout.Token);
            using JsonDocument document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token),
                cancellationToken: timeout.Token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("discweave-api", document.RootElement.GetProperty("service").GetString());
            Assert.Equal("ok", document.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(connectionStringVariableName, previousConnectionString);
        }
    }
}
