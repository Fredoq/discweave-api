using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class Roadmap08NavigationEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public Roadmap08NavigationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Credit endpoints include target titles in create get update and list responses")]
    public async Task Credit_endpoints_include_target_titles_in_create_get_update_and_list_responses()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid releaseId = await CreateReleaseAsync(client, "Confusion");
        Guid trackId = await CreateTrackAsync(client, "Confusion (Instrumental)");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId = artistId, targetType = "release", targetId = releaseId, role = "producer" });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid creditId = createDocument.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Confusion", createDocument.RootElement.GetProperty("targetTitle").GetString());

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/credits/{creditId}",
            new { contributorArtistId = artistId, targetType = "track", targetId = trackId, role = "remixer" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);
        Assert.Equal("Confusion (Instrumental)", updateDocument.RootElement.GetProperty("targetTitle").GetString());

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/credits/{creditId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);
        Assert.Equal("Confusion (Instrumental)", getDocument.RootElement.GetProperty("targetTitle").GetString());

        using HttpResponseMessage listResponse = await client.GetAsync($"/api/credits?contributorArtistId={artistId}&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        JsonElement credit = Assert.Single(listDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Confusion (Instrumental)", credit.GetProperty("targetTitle").GetString());
    }

    [Fact(DisplayName = "Relation endpoints include artist names and track titles")]
    public async Task Relation_endpoints_include_artist_names_and_track_titles()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Bernard Sumner");
        Guid groupId = await CreateArtistAsync(client, "New Order", "group");
        Guid remixId = await CreateTrackAsync(client, "Blue Monday (Hardfloor Mix)");
        Guid originalId = await CreateTrackAsync(client, "Blue Monday");

        using HttpResponseMessage artistCreateResponse = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId = artistId, targetArtistId = groupId, type = "memberOf", startYear = 1980 });
        Assert.Equal(HttpStatusCode.Created, artistCreateResponse.StatusCode);
        using JsonDocument artistCreateDocument = await ReadJsonAsync(artistCreateResponse);
        Guid artistRelationId = artistCreateDocument.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Bernard Sumner", artistCreateDocument.RootElement.GetProperty("sourceArtistName").GetString());
        Assert.Equal("New Order", artistCreateDocument.RootElement.GetProperty("targetArtistName").GetString());

        using HttpResponseMessage artistListResponse = await client.GetAsync($"/api/artist-relations?sourceArtistId={artistId}&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, artistListResponse.StatusCode);
        using JsonDocument artistListDocument = await ReadJsonAsync(artistListResponse);
        JsonElement artistRelation = Assert.Single(artistListDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(artistRelationId, artistRelation.GetProperty("id").GetGuid());
        Assert.Equal("New Order", artistRelation.GetProperty("targetArtistName").GetString());

        using HttpResponseMessage trackCreateResponse = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId = remixId, targetTrackId = originalId, type = "remixOf" });
        Assert.Equal(HttpStatusCode.Created, trackCreateResponse.StatusCode);
        using JsonDocument trackCreateDocument = await ReadJsonAsync(trackCreateResponse);
        Guid trackRelationId = trackCreateDocument.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Blue Monday (Hardfloor Mix)", trackCreateDocument.RootElement.GetProperty("sourceTrackTitle").GetString());
        Assert.Equal("Blue Monday", trackCreateDocument.RootElement.GetProperty("targetTrackTitle").GetString());

        using HttpResponseMessage trackListResponse = await client.GetAsync($"/api/track-relations?targetTrackId={originalId}&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, trackListResponse.StatusCode);
        using JsonDocument trackListDocument = await ReadJsonAsync(trackListResponse);
        JsonElement trackRelation = Assert.Single(trackListDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(trackRelationId, trackRelation.GetProperty("id").GetGuid());
        Assert.Equal("Blue Monday (Hardfloor Mix)", trackRelation.GetProperty("sourceTrackTitle").GetString());
    }

    [Fact(DisplayName = "Catalog graph exposes related artists track relations and credited track appearances")]
    public async Task Catalog_graph_exposes_related_artists_track_relations_and_credited_track_appearances()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Arthur Baker");
        Guid aliasId = await CreateArtistAsync(client, "Planet Patrol", "group");
        Guid originalTrackId = await CreateTrackAsync(client, "Confusion");
        Guid remixTrackId = await CreateTrackAsync(client, "Confusion (Instrumental)");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, "Confusion", remixTrackId);
        _ = await CreateCreditAsync(client, artistId, "track", remixTrackId, "remixer");
        _ = await CreateArtistRelationAsync(client, artistId, aliasId, "alias");
        _ = await CreateTrackRelationAsync(client, remixTrackId, originalTrackId, "remixOf");

        using HttpResponseMessage artistGraphResponse = await client.GetAsync($"/api/catalog-graph/artist/{artistId}");
        Assert.Equal(HttpStatusCode.OK, artistGraphResponse.StatusCode);
        using JsonDocument artistGraphDocument = await ReadJsonAsync(artistGraphResponse);
        JsonElement artistSections = artistGraphDocument.RootElement.GetProperty("sections");
        Assert.Contains(artistSections.GetProperty("artists").EnumerateArray(), link => link.GetProperty("id").GetGuid() == aliasId);
        Assert.Contains(artistSections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);

        using HttpResponseMessage trackGraphResponse = await client.GetAsync($"/api/catalog-graph/track/{remixTrackId}");
        Assert.Equal(HttpStatusCode.OK, trackGraphResponse.StatusCode);
        using JsonDocument trackGraphDocument = await ReadJsonAsync(trackGraphResponse);
        JsonElement trackSections = trackGraphDocument.RootElement.GetProperty("sections");
        Assert.Contains(trackSections.GetProperty("tracks").EnumerateArray(), link => link.GetProperty("id").GetGuid() == originalTrackId);
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
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithTrackAsync(HttpClient client, string title, Guid trackId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "standalone",
                isVariousArtists = true,
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
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/credits", new { contributorArtistId, targetType, targetId, role });
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
}
