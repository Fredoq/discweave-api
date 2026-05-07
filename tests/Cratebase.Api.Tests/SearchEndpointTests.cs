using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class SearchEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectroGenres = ["Electro"];
    private static readonly string[] FactoryTags = ["factory"];
    private static readonly string[] RemixTags = ["remix"];
    private readonly PostgresFixture _postgres;

    public SearchEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Search finds catalog graph and collection ownership signals")]
    public async Task Search_finds_catalog_graph_and_collection_ownership_signals()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid releaseId = await CreateReleaseAsync(client, "Confusion", labelId, FactoryTags);
        Guid trackId = await CreateTrackAsync(client, "Confusion (Instrumental)", RemixTags);
        _ = await CreateCreditAsync(client, artistId, "release", releaseId, "producer");
        _ = await CreateCreditAsync(client, artistId, "track", trackId, "remixer");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "needsDigitization");

        await AssertSearchResultAsync(client, "arthur", "artist", artistId, "name");
        await AssertSearchResultAsync(client, "factory", "label", labelId, "name");
        await AssertSearchResultAsync(client, "factory", "release", releaseId, "label");
        await AssertSearchResultAsync(client, "instrumental", "track", trackId, "title");
        await AssertSearchResultAsync(client, "producer", "release", releaseId, "credit.role");
        await AssertSearchResultAsync(client, "remixer", "track", trackId, "credit.role");
        await AssertSearchResultAsync(client, "needsDigitization", "ownedItem", ownedItemId, "ownershipStatus");
    }

    [Fact(DisplayName = "Search only returns results from the current collection")]
    public async Task Search_only_returns_results_from_the_current_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Private Search Marker");
        Guid userArtistId = await CreateArtistAsync(userClient, "Private Search Marker");

        using HttpResponseMessage response = await userClient.GetAsync("/api/search?query=Private%20Search%20Marker&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement item = document.RootElement.GetProperty("items")[0];
        Assert.Equal("artist", item.GetProperty("type").GetString());
        Assert.Equal(userArtistId, item.GetProperty("id").GetGuid());
        Assert.NotEqual(adminReleaseId, item.GetProperty("id").GetGuid());
    }

    private static async Task AssertSearchResultAsync(HttpClient client, string query, string expectedType, Guid expectedId, string expectedMatchedField)
    {
        using HttpResponseMessage response = await client.GetAsync($"/api/search?query={Uri.EscapeDataString(query)}&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == expectedType && result.GetProperty("id").GetGuid() == expectedId);
        Assert.Contains(
            expectedMatchedField,
            item.GetProperty("matchedFields").EnumerateArray().Select(field => field.GetString()));
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

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid? labelId = null, IReadOnlyList<string>? tags = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, labelId, year = 1983, genres = ElectroGenres, tags = tags ?? [] });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title, IReadOnlyList<string> tags)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, durationSeconds = 360, genres = ElectroGenres, tags });
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

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status,
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
}
