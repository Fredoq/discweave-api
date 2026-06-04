using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class CatalogGraphNavigationEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public CatalogGraphNavigationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Catalog graph context describes label releases and owned coverage")]
    public async Task Catalog_graph_context_describes_label_releases_and_owned_coverage()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid releaseId = await CreateReleaseAsync(client, "Blue Monday", labelId);
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "owned", "vinyl");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/label/{labelId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("label", document.RootElement.GetProperty("entity").GetProperty("type").GetString());
        Assert.Equal(labelId, document.RootElement.GetProperty("entity").GetProperty("id").GetGuid());
        JsonElement releaseLink = Assert.Single(document.RootElement.GetProperty("sections").GetProperty("releases").EnumerateArray());
        Assert.Equal(releaseId, releaseLink.GetProperty("id").GetGuid());
        JsonElement ownedCopyLink = Assert.Single(document.RootElement.GetProperty("sections").GetProperty("ownedCopies").EnumerateArray());
        Assert.Equal(ownedItemId, ownedCopyLink.GetProperty("id").GetGuid());
        Assert.Contains(document.RootElement.GetProperty("collectorSignals").EnumerateArray(), signal => signal.GetString() == "vinyl");
    }

    [Fact(DisplayName = "Catalog graph context describes artist credits and relations")]
    public async Task Catalog_graph_context_describes_artist_credits_and_relations()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid relatedArtistId = await CreateArtistAsync(client, "Planet Patrol");
        Guid releaseId = await CreateReleaseAsync(client, "Confusion");
        Guid relationId = await CreateArtistRelationAsync(client, artistId, relatedArtistId, "alias");
        _ = await CreateCreditAsync(client, artistId, "release", releaseId, "producer");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/artist/{artistId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("artist", document.RootElement.GetProperty("entity").GetProperty("type").GetString());
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("relations").EnumerateArray(), link => link.GetProperty("id").GetGuid() == relationId);
    }

    [Fact(DisplayName = "Catalog graph context describes release credits tracks labels and media")]
    public async Task Catalog_graph_context_describes_release_credits_tracks_labels_and_media()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Tommy Boy");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid trackId = await CreateTrackAsync(client, "Confusion");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, "Confusion", trackId, labelId);
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "owned", "vinyl");
        _ = await CreateCreditAsync(client, artistId, "release", releaseId, "mainArtist");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/release/{releaseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement sections = document.RootElement.GetProperty("sections");
        Assert.Contains(sections.GetProperty("artists").EnumerateArray(), link => link.GetProperty("id").GetGuid() == artistId);
        Assert.Contains(sections.GetProperty("tracks").EnumerateArray(), link => link.GetProperty("id").GetGuid() == trackId);
        Assert.Contains(sections.GetProperty("labels").EnumerateArray(), link => link.GetProperty("id").GetGuid() == labelId);
        Assert.Contains(sections.GetProperty("ownedCopies").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
        Assert.Contains(sections.GetProperty("media").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
    }

    [Fact(DisplayName = "Catalog graph context describes track appearances relations and owned media")]
    public async Task Catalog_graph_context_describes_track_appearances_relations_and_owned_media()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid artistId = await CreateArtistAsync(client, "Hardfloor");
        Guid trackId = await CreateTrackAsync(client, "Blue Monday (Hardfloor Mix)");
        Guid originalTrackId = await CreateTrackAsync(client, "Blue Monday");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, "Blue Monday Remixes", trackId);
        Guid relationId = await CreateTrackRelationAsync(client, trackId, originalTrackId, "remixOf");
        Guid ownedItemId = await CreateOwnedItemAsync(client, "track", trackId, "needsDigitization", "cassette");
        _ = await CreateCreditAsync(client, artistId, "track", trackId, "remixer");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/track/{trackId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement sections = document.RootElement.GetProperty("sections");
        Assert.Contains(sections.GetProperty("artists").EnumerateArray(), link => link.GetProperty("id").GetGuid() == artistId);
        Assert.Contains(sections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains(sections.GetProperty("relations").EnumerateArray(), link => link.GetProperty("id").GetGuid() == relationId);
        Assert.Contains(sections.GetProperty("ownedCopies").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
        Assert.Contains(document.RootElement.GetProperty("collectorSignals").EnumerateArray(), signal => signal.GetString() == "needsDigitization");
    }

    [Fact(DisplayName = "Catalog graph context describes owned item targets")]
    public async Task Catalog_graph_context_describes_owned_item_targets()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid releaseId = await CreateReleaseAsync(client, "Owned Target Release");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "sold", "cd");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/owned-item/{ownedItemId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("ownedItem", document.RootElement.GetProperty("entity").GetProperty("type").GetString());
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("media").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
    }

    [Fact(DisplayName = "Catalog graph context only returns entities from the current collection")]
    public async Task Catalog_graph_context_only_returns_entities_from_the_current_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminLabelId = await CreateLabelAsync(adminClient, "Private Label");

        using HttpResponseMessage response = await userClient.GetAsync($"/api/catalog-graph/label/{adminLabelId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid? labelId = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, labelId, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithTrackAsync(HttpClient client, string title, Guid trackId, Guid? labelId = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "standalone",
                isVariousArtists = true,
                labelId,
                year = 1983,
                genres = EmptyStrings,
                tags = EmptyStrings,
                tracklist = new[] { new { trackId, position = 1, versionNote = (string?)null } }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title, genres = EmptyStrings, tags = EmptyStrings });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, string targetType, Guid targetId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId, targetType, targetId, roles = new[] { role } });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status, string medium)
    {
        return await CreateOwnedItemAsync(client, "release", releaseId, status, medium);
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, string targetType, Guid targetId, string status, string medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/owned-items", new { targetType, targetId, status, medium = new { type = medium, description = medium } });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistRelationAsync(HttpClient client, Guid sourceArtistId, Guid targetArtistId, string type)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artist-relations", new { sourceArtistId, targetArtistId, type });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackRelationAsync(HttpClient client, Guid sourceTrackId, Guid targetTrackId, string type)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/track-relations", new { sourceTrackId, targetTrackId, type });
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
