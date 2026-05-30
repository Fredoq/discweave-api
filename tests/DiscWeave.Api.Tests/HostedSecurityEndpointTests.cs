using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class HostedSecurityEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public HostedSecurityEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Production responses include hosted security headers")]
    public async Task Production_responses_include_hosted_security_headers()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres, environmentName: "Production");
        HttpClient client = host.CreateClient(new Uri("https://discweave.example.test"));

        using HttpResponseMessage response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Contains("DENY", response.Headers.GetValues("X-Frame-Options"));
        Assert.Contains("no-referrer", response.Headers.GetValues("Referrer-Policy"));
        Assert.True(response.Headers.Contains("Strict-Transport-Security"));
    }

    [Fact(DisplayName = "Hosted unsafe requests reject untrusted origins")]
    public async Task Hosted_unsafe_requests_reject_untrusted_origins()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres, environmentName: "Production");
        HttpClient client = host.CreateClient(new Uri("https://discweave.example.test"));
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "missing@example.com", password = "Password1!" })
        };
        request.Headers.Add("Origin", "https://evil.example");

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("security.origin_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Hosted unsafe requests reject ambiguous origins")]
    public async Task Hosted_unsafe_requests_reject_ambiguous_origins()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres, environmentName: "Production");
        HttpClient client = host.CreateClient(new Uri("https://discweave.example.test"));
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "missing@example.com", password = "Password1!" })
        };
        request.Headers.Add("Origin", ["https://discweave.example.test", "https://evil.example"]);

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("security.origin_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Hosted unsafe requests allow forwarded same-origin requests")]
    public async Task Hosted_unsafe_requests_allow_forwarded_same_origin_requests()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres, environmentName: "Production");
        HttpClient client = host.CreateClient(new Uri("http://internal.test"));
        using HttpRequestMessage request = new(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email = "missing@example.com", password = "Password1!" })
        };
        request.Headers.Add("Origin", "https://discweave.example.test");
        request.Headers.Add("X-Forwarded-Host", "discweave.example.test");
        request.Headers.Add("X-Forwarded-Proto", "https");

        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Auth endpoints return structured rate limit errors")]
    public async Task Auth_endpoints_return_structured_rate_limit_errors()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres, environmentName: "Production");
        HttpClient client = host.CreateClient(new Uri("https://discweave.example.test"));
        HttpResponseMessage? lastResponse = null;

        for (int requestIndex = 0; requestIndex < 11; requestIndex++)
        {
            lastResponse?.Dispose();
            lastResponse = await client.PostAsJsonAsync(
                "/api/auth/login",
                new { email = "missing@example.com", password = "Password1!" });
        }

        using HttpResponseMessage response = lastResponse ?? throw new InvalidOperationException("Rate limit request was not sent");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("rate_limit.exceeded", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(content);
    }
}
