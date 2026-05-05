using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cratebase.Domain.SharedKernel.Ids;

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

    [Fact(DisplayName = "Session reports bootstrap required when no users exist")]
    public async Task Session_reports_bootstrap_required_when_no_users_exist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/auth/session");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(document.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("bootstrapRequired").GetBoolean());
        Assert.Empty(document.RootElement.GetProperty("roles").EnumerateArray());
        Assert.False(document.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(document.RootElement.TryGetProperty("defaultCollectionId", out _));
    }

    [Fact(DisplayName = "First registration creates an admin session with a default collection")]
    public async Task First_registration_creates_an_admin_session_with_a_default_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using JsonDocument registerDocument = await ReadJsonAsync(registerResponse);
        using HttpResponseMessage sessionResponse = await client.GetAsync("/api/auth/session");
        using JsonDocument sessionDocument = await ReadJsonAsync(sessionResponse);
        CollectionId? defaultCollectionId = await host.FindDefaultCollectionIdForUserAsync("owner@example.com");

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.True(registerDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("owner@example.com", registerDocument.RootElement.GetProperty("email").GetString());
        Assert.Contains("Admin", registerDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(registerDocument.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(registerDocument.RootElement.TryGetProperty("defaultCollectionId", out _));
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.True(sessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("owner@example.com", sessionDocument.RootElement.GetProperty("email").GetString());
        Assert.True(defaultCollectionId.HasValue);
        Assert.True(await host.CollectionExistsAsync(defaultCollectionId.Value));
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
        using HttpResponseMessage anonymousSessionResponse = await client.GetAsync("/api/auth/session");
        using JsonDocument anonymousSessionDocument = await ReadJsonAsync(anonymousSessionResponse);
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("owner@example.com", "Password1!"));
        using JsonDocument loginDocument = await ReadJsonAsync(loginResponse);
        using HttpResponseMessage authenticatedSessionResponse = await client.GetAsync("/api/auth/session");
        using JsonDocument authenticatedSessionDocument = await ReadJsonAsync(authenticatedSessionResponse);
        using HttpResponseMessage secondLogoutResponse = await client.PostAsync("/api/auth/logout", null);
        using HttpResponseMessage signedOutSessionResponse = await client.GetAsync("/api/auth/session");
        using JsonDocument signedOutSessionDocument = await ReadJsonAsync(signedOutSessionResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, firstLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, anonymousSessionResponse.StatusCode);
        Assert.False(anonymousSessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.False(anonymousSessionDocument.RootElement.GetProperty("bootstrapRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.True(loginDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, authenticatedSessionResponse.StatusCode);
        Assert.True(authenticatedSessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("owner@example.com", authenticatedSessionDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NoContent, secondLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, signedOutSessionResponse.StatusCode);
        Assert.False(signedOutSessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact(DisplayName = "Login rejects invalid credentials")]
    public async Task Login_rejects_invalid_credentials()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage logoutResponse = await client.PostAsync("/api/auth/logout", null);
        using HttpResponseMessage loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("owner@example.com", "incorrect"));
        using JsonDocument loginDocument = await ReadJsonAsync(loginResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
        Assert.Equal("auth.invalid_credentials", loginDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Login rejects an unknown email with the invalid credentials contract")]
    public async Task Login_rejects_an_unknown_email_with_the_invalid_credentials_contract()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("missing@example.com", "Password1!"));
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.invalid_credentials", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Me returns authenticated user data without collection identifiers")]
    public async Task Me_returns_authenticated_user_data_without_collection_identifiers()
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
        Assert.True(meDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal("owner@example.com", meDocument.RootElement.GetProperty("email").GetString());
        Assert.Contains("Admin", meDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(meDocument.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(meDocument.RootElement.TryGetProperty("defaultCollectionId", out _));
    }

    [Fact(DisplayName = "Disabled users cannot login or keep an existing cookie")]
    public async Task Disabled_users_cannot_login_or_keep_an_existing_cookie()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient userClient = host.CreateClient();

        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        using JsonDocument createUserDocument = await ReadJsonAsync(createUserResponse);
        Guid userId = createUserDocument.RootElement.GetProperty("id").GetGuid();
        using HttpResponseMessage firstLoginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        using HttpResponseMessage disableResponse = await adminClient.PatchAsJsonAsync(
            $"/api/admin/users/{userId}/status",
            new UpdateUserStatusRequest(true));
        using HttpResponseMessage sessionResponse = await userClient.GetAsync("/api/auth/session");
        using JsonDocument sessionDocument = await ReadJsonAsync(sessionResponse);
        HttpClient disabledClient = host.CreateClient();
        using HttpResponseMessage disabledLoginResponse = await disabledClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        using JsonDocument disabledLoginDocument = await ReadJsonAsync(disabledLoginResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, firstLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.False(sessionDocument.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.False(sessionDocument.RootElement.GetProperty("bootstrapRequired").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, disabledLoginResponse.StatusCode);
        Assert.Equal("auth.user_disabled", disabledLoginDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Session treats invalid cookies as unauthenticated")]
    public async Task Session_treats_invalid_cookies_as_unauthenticated()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = host.CreateClient();

        using HttpRequestMessage request = new(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", "Cratebase.Auth=invalid");
        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(document.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("bootstrapRequired").GetBoolean());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

    private sealed record UpdateUserStatusRequest(bool IsDisabled);
}
