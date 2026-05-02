using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class AuthEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public AuthEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Anonymous collection requests are rejected")]
    public async Task Anonymous_collection_requests_are_rejected()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new AuthRequest("anonymous@example.com", "Password1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName = "First registration creates an admin session with a default collection")]
    public async Task First_registration_creates_an_admin_session_with_a_default_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage meResponse = await client.GetAsync("/api/auth/me");
        using JsonDocument meDocument = await ReadJsonAsync(meResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal("owner@example.com", meDocument.RootElement.GetProperty("email").GetString());
        Assert.Contains("Admin", meDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.NotEqual(Guid.Empty, meDocument.RootElement.GetProperty("defaultCollectionId").GetGuid());
    }

    [Fact(DisplayName = "Public registration closes after the first user exists")]
    public async Task Public_registration_closes_after_the_first_user_exists()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient firstClient = host.CreateClient();
        HttpClient secondClient = host.CreateClient();

        using HttpResponseMessage firstResponse = await firstClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage secondResponse = await secondClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("second@example.com", "Password1!"));
        using JsonDocument secondDocument = await ReadJsonAsync(secondResponse);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Equal("auth.registration_closed", secondDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Login and logout update the current user cookie session")]
    public async Task Login_and_logout_update_the_current_user_cookie_session()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage firstLogoutResponse = await client.PostAsync("/api/auth/logout", null);
        using HttpResponseMessage anonymousMeResponse = await client.GetAsync("/api/auth/me");
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage authenticatedMeResponse = await client.GetAsync("/api/auth/me");
        using HttpResponseMessage secondLogoutResponse = await client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, firstLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticatedMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondLogoutResponse.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);
}
