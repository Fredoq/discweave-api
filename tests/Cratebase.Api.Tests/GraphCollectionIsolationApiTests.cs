using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class GraphCollectionIsolationApiTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public GraphCollectionIsolationApiTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Users cannot read list update or delete graph records from another collection")]
    public async Task Users_cannot_read_list_update_or_delete_graph_records_from_another_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminArtistId = await CreateArtistAsync(adminClient, "Arthur Baker");
        Guid adminGroupId = await CreateArtistAsync(adminClient, "New Order", "group");
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Confusion");
        Guid adminTrackId = await CreateTrackAsync(adminClient, "Confusion");
        Guid adminOtherTrackId = await CreateTrackAsync(adminClient, "Confusion (Instrumental)");
        Guid adminCreditId = await CreateCreditAsync(adminClient, adminArtistId, "release", adminReleaseId, "producer");
        Guid adminArtistRelationId = await CreateArtistRelationAsync(adminClient, adminArtistId, adminGroupId);
        Guid adminTrackRelationId = await CreateTrackRelationAsync(adminClient, adminOtherTrackId, adminTrackId);

        Guid userArtistId = await CreateArtistAsync(userClient, "Arthur Baker");
        Guid userGroupId = await CreateArtistAsync(userClient, "New Order", "group");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "Confusion");
        Guid userTrackId = await CreateTrackAsync(userClient, "Confusion");
        Guid userOtherTrackId = await CreateTrackAsync(userClient, "Confusion (Instrumental)");
        Guid userCreditId = await CreateCreditAsync(userClient, userArtistId, "release", userReleaseId, "producer");
        Guid userArtistRelationId = await CreateArtistRelationAsync(userClient, userArtistId, userGroupId);
        Guid userTrackRelationId = await CreateTrackRelationAsync(userClient, userOtherTrackId, userTrackId);

        await AssertGraphRecordIsIsolatedAsync(
            userClient,
            "/api/credits",
            adminCreditId,
            userCreditId,
            new { contributorArtistId = userArtistId, targetType = "release", targetId = userReleaseId, role = "producer" },
            "credit");
        await AssertGraphRecordIsIsolatedAsync(
            userClient,
            "/api/artist-relations",
            adminArtistRelationId,
            userArtistRelationId,
            new { sourceArtistId = userArtistId, targetArtistId = userGroupId, type = "memberOf", startYear = (int?)null, endYear = (int?)null },
            "artist-relation");
        await AssertGraphRecordIsIsolatedAsync(
            userClient,
            "/api/track-relations",
            adminTrackRelationId,
            userTrackRelationId,
            new { sourceTrackId = userOtherTrackId, targetTrackId = userTrackId, type = "remixOf" },
            "track-relation");
    }

    private static async Task AssertGraphRecordIsIsolatedAsync(
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
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync("/api/auth/register", new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync("/api/auth/login", new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, string type = "person")
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type, name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/releases", new { title, type = "standalone", isVariousArtists = true });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, string targetType, Guid targetId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/credits", new { contributorArtistId, targetType, targetId, role });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistRelationAsync(HttpClient client, Guid sourceArtistId, Guid targetArtistId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId, targetArtistId, type = "memberOf", startYear = (int?)null, endYear = (int?)null });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackRelationAsync(HttpClient client, Guid sourceTrackId, Guid targetTrackId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/track-relations", new { sourceTrackId, targetTrackId, type = "remixOf" });
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
}
