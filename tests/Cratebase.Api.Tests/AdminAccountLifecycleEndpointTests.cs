using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class AdminAccountLifecycleEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public AdminAccountLifecycleEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Admin created users are ordinary users without collection identifiers")]
    public async Task Admin_created_users_are_ordinary_users_without_collection_identifiers()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!"));
        using JsonDocument createUserDocument = await ReadJsonAsync(createUserResponse);
        using HttpResponseMessage listUsersResponse = await adminClient.GetAsync("/api/admin/users");
        using JsonDocument listUsersDocument = await ReadJsonAsync(listUsersResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal("collector@example.com", createUserDocument.RootElement.GetProperty("email").GetString());
        Assert.Equal(["User"], createUserDocument.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString()));
        Assert.False(createUserDocument.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(createUserDocument.RootElement.TryGetProperty("defaultCollectionId", out _));
        Assert.Equal(HttpStatusCode.OK, listUsersResponse.StatusCode);
        Assert.DoesNotContain(
            listUsersDocument.RootElement.EnumerateArray(),
            user => user.TryGetProperty("collectionId", out _) || user.TryGetProperty("defaultCollectionId", out _));
    }

    [Fact(DisplayName = "Admins cannot disable the last active admin")]
    public async Task Admins_cannot_disable_the_last_active_admin()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        Guid adminId = await FindUserIdAsync(adminClient, "owner@example.com");
        using HttpResponseMessage disableResponse = await adminClient.PatchAsJsonAsync(
            $"/api/admin/users/{adminId}/status",
            new UpdateUserStatusRequest(true));
        using JsonDocument disableDocument = await ReadJsonAsync(disableResponse);

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, disableResponse.StatusCode);
        Assert.Equal("user.last_admin", disableDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Admin password reset and self password change update login credentials")]
    public async Task Admin_password_reset_and_self_password_change_update_login_credentials()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient userClient = host.CreateClient();
        HttpClient oldPasswordClient = host.CreateClient();
        HttpClient changedPasswordClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!"));
        using JsonDocument createUserDocument = await ReadJsonAsync(createUserResponse);
        Guid userId = createUserDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage resetResponse = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{userId}/password",
            new AdminPasswordRequest("Temporary1!"));
        using HttpResponseMessage oldLoginResponse = await oldPasswordClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        using HttpResponseMessage resetLoginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Temporary1!"));
        using HttpResponseMessage changePasswordResponse = await userClient.PostAsJsonAsync(
            "/api/auth/password",
            new ChangePasswordRequest("Temporary1!", "Changed1!"));
        using HttpResponseMessage changedLoginResponse = await changedPasswordClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Changed1!"));

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, resetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resetLoginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changedLoginResponse.StatusCode);
    }

    [Fact(DisplayName = "Non admin users cannot manage invites users or passwords")]
    public async Task Non_admin_users_cannot_manage_invites_users_or_passwords()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        HttpClient userClient = host.CreateClient();

        using HttpResponseMessage bootstrapResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!"));
        using JsonDocument createUserDocument = await ReadJsonAsync(createUserResponse);
        Guid userId = createUserDocument.RootElement.GetProperty("id").GetGuid();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));

        using HttpResponseMessage listInvitesResponse = await userClient.GetAsync("/api/admin/invites");
        using HttpResponseMessage createInviteResponse = await userClient.PostAsJsonAsync(
            "/api/admin/invites",
            new CreateInviteRequest(null, null));
        using HttpResponseMessage revokeInviteResponse = await userClient.PostAsync($"/api/admin/invites/{Guid.CreateVersion7()}/revoke", null);
        using HttpResponseMessage listUsersResponse = await userClient.GetAsync("/api/admin/users");
        using HttpResponseMessage setPasswordResponse = await userClient.PostAsJsonAsync(
            $"/api/admin/users/{userId}/password",
            new AdminPasswordRequest("Temporary1!"));

        Assert.Equal(HttpStatusCode.Created, bootstrapResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listInvitesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createInviteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revokeInviteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listUsersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, setPasswordResponse.StatusCode);
    }

    private static async Task<Guid> FindUserIdAsync(HttpClient adminClient, string email)
    {
        using HttpResponseMessage response = await adminClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement user = document.RootElement.EnumerateArray().Single(item => item.GetProperty("email").GetString() == email);

        return user.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password);

    private sealed record UpdateUserStatusRequest(bool IsDisabled);

    private sealed record AdminPasswordRequest(string TemporaryPassword);

    private sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    private sealed record CreateInviteRequest(DateTimeOffset? ExpiresAt, string? Note);
}
