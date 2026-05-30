using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed partial class PlaylistEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private static readonly string[] CrateTags = ["crate"];
    private static readonly string[] ElectronicGenres = ["Electronic"];
    private static readonly string[] RadioTags = ["radio"];
    private static readonly string[] DigitalMedia = ["digital"];
    private static readonly string[] OwnedStatuses = ["owned"];
    private readonly PostgresFixture _postgres;

    public PlaylistEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Manual playlists preserve explicit track and release order")]
    public async Task Manual_playlists_preserve_explicit_track_and_release_order()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Archive Twelve");
        Guid trackId = await CreateTrackAsync(client, "Locked Groove");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name = "Set notes",
                type = "manual",
                entries = new object[]
                {
                    new { kind = "release", id = releaseId },
                    new { kind = "track", id = trackId }
                }
            });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument created = await ReadJsonAsync(createResponse);
        Guid playlistId = created.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/playlists/{playlistId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument document = await ReadJsonAsync(getResponse);
        JsonElement[] entries = [.. document.RootElement.GetProperty("entries").EnumerateArray()];
        Assert.Equal("release", entries[0].GetProperty("kind").GetString());
        Assert.Equal(releaseId, entries[0].GetProperty("id").GetGuid());
        Assert.Equal("track", entries[1].GetProperty("kind").GetString());
        Assert.Equal(trackId, entries[1].GetProperty("id").GetGuid());
        Assert.Equal("Archive Twelve", entries[0].GetProperty("title").GetString());
        Assert.Equal("Locked Groove", entries[1].GetProperty("title").GetString());
    }

    [Fact(DisplayName = "Smart playlists compute dynamic results from ANDed rule categories")]
    public async Task Smart_playlists_compute_dynamic_results_from_anded_rule_categories()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid matchingReleaseId = await CreateReleaseAsync(client, "Cold Storage", tags: CrateTags, genres: ElectronicGenres, year: 1998);
        _ = await CreateDigitalOwnedItemAsync(client, matchingReleaseId, "owned", "flac");
        Guid wrongTagReleaseId = await CreateReleaseAsync(client, "Bright Storage", tags: RadioTags, genres: ElectronicGenres, year: 1998);
        _ = await CreateDigitalOwnedItemAsync(client, wrongTagReleaseId, "owned", "flac");
        Guid wrongMediumReleaseId = await CreateReleaseAsync(client, "Cold Plate", tags: CrateTags, genres: ElectronicGenres, year: 1998);
        _ = await CreateOwnedItemAsync(client, wrongMediumReleaseId, "owned", "vinyl");

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name = "Lossless dub crate",
                type = "smart",
                rules = new
                {
                    tags = CrateTags,
                    genres = ElectronicGenres,
                    media = DigitalMedia,
                    ownershipStatuses = OwnedStatuses,
                    yearFrom = 1990,
                    yearTo = 1999
                }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement result = Assert.Single(document.RootElement.GetProperty("results").EnumerateArray());
        Assert.Equal("release", result.GetProperty("kind").GetString());
        Assert.Equal(matchingReleaseId, result.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "Playlist delete requires an exact confirmation token")]
    public async Task Playlist_delete_requires_an_exact_confirmation_token()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid playlistId = await CreatePlaylistAsync(client, "Delete me");

        using HttpResponseMessage rejected = await client.DeleteAsync($"/api/playlists/{playlistId}");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/playlists/{playlistId}");
        request.Headers.Add("X-DiscWeave-Confirm-Delete", $"playlist:{playlistId}");
        using HttpResponseMessage deleted = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact(DisplayName = "Playlist routes are scoped to the authenticated collection")]
    public async Task Playlist_routes_are_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient firstClient, HttpClient secondClient) = await CreateAuthenticatedClientsAsync(host);
        Guid playlistId = await CreatePlaylistAsync(firstClient, "Private list");

        using HttpResponseMessage getResponse = await secondClient.GetAsync($"/api/playlists/{playlistId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        using HttpResponseMessage listResponse = await secondClient.GetAsync("/api/playlists");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using JsonDocument document = await ReadJsonAsync(listResponse);
        Assert.Equal(0, document.RootElement.GetProperty("total").GetInt32());
    }

    private static async Task<Guid> CreatePlaylistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new { name, type = "manual", entries = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpClient FirstClient, HttpClient SecondClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
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

    private static async Task<Guid> CreateReleaseAsync(
        HttpClient client,
        string title,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? genres = null,
        int year = 1983)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "standalone",
                isVariousArtists = true,
                year,
                genres = genres ?? EmptyStrings,
                tags = tags ?? EmptyStrings
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, genres = EmptyStrings, tags = EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status, string medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status,
                medium = new { type = medium, description = medium }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateDigitalOwnedItemAsync(HttpClient client, Guid releaseId, string status, string format)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status,
                medium = new
                {
                    type = "digital",
                    path = $"/music/{releaseId:N}-{format}.audio",
                    format
                }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
}
