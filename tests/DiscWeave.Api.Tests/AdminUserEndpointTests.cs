using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class AdminUserEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public AdminUserEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Admins can create users and non admins cannot list users")]
    public async Task Admins_can_create_users_and_non_admins_cannot_list_users()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();

        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        using JsonDocument createUserDocument = await ReadJsonAsync(createUserResponse);
        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        using HttpResponseMessage userListResponse = await userClient.GetAsync("/api/admin/users");
        using HttpResponseMessage adminListResponse = await adminClient.GetAsync("/api/admin/users");
        using JsonDocument adminListDocument = await ReadJsonAsync(adminListResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal("collector@example.com", createUserDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, userListResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminListResponse.StatusCode);
        Assert.Equal(2, adminListDocument.RootElement.GetArrayLength());
    }

    [Fact(DisplayName = "Disabling a user revokes their existing cookie")]
    public async Task Disabling_a_user_revokes_their_existing_cookie()
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
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        using HttpResponseMessage disableResponse = await adminClient.PatchAsJsonAsync(
            $"/api/admin/users/{userId}/status",
            new UpdateUserStatusRequest(true));
        using HttpResponseMessage catalogResponse = await userClient.GetAsync("/api/artists");

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, catalogResponse.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

    private sealed record UpdateUserStatusRequest(bool IsDisabled);
}
