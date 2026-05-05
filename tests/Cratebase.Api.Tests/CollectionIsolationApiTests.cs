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

    [Fact(DisplayName = "Users only see and mutate core catalog records from their own collection")]
    public async Task Users_only_see_and_mutate_core_catalog_records_from_their_own_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminLabelId = await CreateLabelAsync(adminClient, "Factory Records");
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Blue Monday", adminLabelId);
        Guid adminTrackId = await CreateTrackAsync(adminClient, "Blue Monday");
        Guid adminOwnedItemId = await CreateOwnedItemAsync(adminClient, adminReleaseId);

        Guid userLabelId = await CreateLabelAsync(userClient, "Factory Records");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "Blue Monday", userLabelId);
        Guid userTrackId = await CreateTrackAsync(userClient, "Blue Monday");
        Guid userOwnedItemId = await CreateOwnedItemAsync(userClient, userReleaseId);

        await AssertCoreRecordIsIsolatedAsync(
            userClient,
            "/api/labels",
            adminLabelId,
            userLabelId,
            new { name = "Factory Records Ltd." },
            "label");
        await AssertCoreRecordIsIsolatedAsync(
            userClient,
            "/api/releases",
            adminReleaseId,
            userReleaseId,
            new { title = "Blue Monday 1988", type = "standalone", labelId = userLabelId, year = 1988 },
            "release");
        await AssertCoreRecordIsIsolatedAsync(
            userClient,
            "/api/tracks",
            adminTrackId,
            userTrackId,
            new { title = "Blue Monday 1988", durationSeconds = 435 },
            "track");
        await AssertCoreRecordIsIsolatedAsync(
            userClient,
            "/api/owned-items",
            adminOwnedItemId,
            userOwnedItemId,
            new { status = "needsDigitization", condition = "veryGood", storageLocation = "Transfer shelf" },
            "owned-item");
    }

    private static async Task AssertCoreRecordIsIsolatedAsync(
        HttpClient client,
        string route,
        Guid foreignId,
        Guid ownId,
        object updateRequest,
        string confirmationResource)
    {
        using HttpResponseMessage getForeignResponse = await client.GetAsync($"{route}/{foreignId}");
        using HttpResponseMessage listResponse = await client.GetAsync($"{route}?limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        using HttpResponseMessage updateForeignResponse = await client.PutAsJsonAsync($"{route}/{foreignId}", updateRequest);
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"{route}/{foreignId}");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"{confirmationResource}:{foreignId}");
        using HttpResponseMessage deleteForeignResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.NotFound, getForeignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(ownId, listDocument.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, updateForeignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deleteForeignResponse.StatusCode);
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid labelId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", labelId, year = 1983 });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title, durationSeconds = 435 });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = "vinyl", description = "12 inch" }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

    private sealed record ArtistRequest(string Type, string Name);
}
