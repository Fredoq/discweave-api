using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class CollectionIsolationApiTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CollectionIsolationApiTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Users only see and mutate artists from their own collection")]
    public async Task Users_only_see_and_mutate_artists_from_their_own_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));

        using HttpResponseMessage adminArtistResponse = await adminClient.PostAsJsonAsync(
            "/api/artists",
            new ArtistRequest("person", "Same Name"));
        using JsonDocument adminArtistDocument = await ReadJsonAsync(adminArtistResponse);
        Guid adminArtistId = adminArtistDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage userArtistResponse = await userClient.PostAsJsonAsync(
            "/api/artists",
            new ArtistRequest("person", "Same Name"));
        using JsonDocument userArtistDocument = await ReadJsonAsync(userArtistResponse);
        Guid userArtistId = userArtistDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage userCannotGetAdminArtistResponse = await userClient.GetAsync($"/api/artists/{adminArtistId}");
        using HttpResponseMessage userListResponse = await userClient.GetAsync("/api/artists?search=Same%20Name&limit=10&offset=0");
        using JsonDocument userListDocument = await ReadJsonAsync(userListResponse);

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminArtistResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, userArtistResponse.StatusCode);
        Assert.NotEqual(adminArtistId, userArtistId);
        Assert.Equal(HttpStatusCode.NotFound, userCannotGetAdminArtistResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, userListResponse.StatusCode);
        Assert.Equal(1, userListDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(userArtistId, userListDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

    private sealed record ArtistRequest(string Type, string Name);
}
