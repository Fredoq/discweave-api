using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ReleaseEntryWorkflowE2ETests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectronicGenres = ["IDM", "Electronic"];
    private static readonly string[] OwnedLaterTags = ["owned later"];
    private readonly PostgresFixture _postgres;

    public ReleaseEntryWorkflowE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Release entry create persists artists labels tracklist and optional ownership")]
    public async Task Release_entry_create_persists_artists_labels_tracklist_and_optional_ownership()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid firstArtistId = await CreateArtistAsync(client, "Autechre");
        Guid secondArtistId = await CreateArtistAsync(client, "The Designers Republic", "group");
        Guid warpLabelId = await CreateLabelAsync(client);

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "  Tri Repetae  ",
                type = "album",
                isVariousArtists = false,
                artistCredits = new object[]
                {
                    new { artistId = firstArtistId, role = "mainArtist" },
                    new { artistId = secondArtistId, role = "producer" }
                },
                labels = new object[]
                {
                    new { labelId = warpLabelId, catalogNumber = "WARPCD38", hasNoCatalogNumber = false },
                    new { name = "Nothing Records", catalogNumber = (string?)null, hasNoCatalogNumber = true }
                },
                notOnLabel = false,
                year = 1995,
                genres = ElectronicGenres,
                tags = OwnedLaterTags,
                tracklist = new object[]
                {
                    new
                    {
                        title = "Dael",
                        position = 1,
                        durationSeconds = 398,
                        artistCredits = Array.Empty<object>(),
                        versionNote = "Original album version"
                    },
                    new
                    {
                        title = "Clipper",
                        position = 2,
                        durationSeconds = (int?)null,
                        artistCredits = new object[] { new { artistId = firstArtistId, role = "mainArtist" } },
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        JsonElement root = createDocument.RootElement;
        Assert.False(root.TryGetProperty("collectionId", out _));
        Assert.Equal("Tri Repetae", root.GetProperty("title").GetString());
        Assert.False(root.GetProperty("isVariousArtists").GetBoolean());
        Assert.Equal(2, root.GetProperty("artistCredits").GetArrayLength());
        Assert.Equal(2, root.GetProperty("labels").GetArrayLength());
        Assert.Equal("WARPCD38", root.GetProperty("labels")[0].GetProperty("catalogNumber").GetString());
        Assert.True(root.GetProperty("labels")[1].GetProperty("hasNoCatalogNumber").GetBoolean());
        Assert.Equal(2, root.GetProperty("tracklist").GetArrayLength());
        Assert.Equal("Dael", root.GetProperty("tracklist")[0].GetProperty("title").GetString());

        using HttpResponseMessage listResponse = await client.GetAsync("/api/releases?search=tri&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        JsonElement listedRelease = listDocument.RootElement.GetProperty("items")[0];
        Assert.False(listedRelease.TryGetProperty("collectionId", out _));
        Assert.Equal(2, listedRelease.GetProperty("artistCredits").GetArrayLength());
        Assert.Equal(2, listedRelease.GetProperty("labels").GetArrayLength());
        Assert.Equal(2, listedRelease.GetProperty("tracklist").GetArrayLength());

        using HttpResponseMessage ownedItemsResponse = await client.GetAsync("/api/owned-items?limit=10&offset=0");
        using JsonDocument ownedItemsDocument = await ReadJsonAsync(ownedItemsResponse);

        Assert.Equal(HttpStatusCode.OK, ownedItemsResponse.StatusCode);
        Assert.Equal(0, ownedItemsDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Release entry create validates title and artist ownership shape")]
    public async Task Release_entry_create_validates_title_and_artist_ownership_shape()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage emptyTitleResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = " ",
                type = "album",
                isVariousArtists = false,
                artistCredits = Array.Empty<object>(),
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = Array.Empty<object>(),
                ownedCopy = (object?)null
            });
        using JsonDocument emptyTitleDocument = await ReadJsonAsync(emptyTitleResponse);

        using HttpResponseMessage missingArtistResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Untitled Private Press",
                type = "album",
                isVariousArtists = false,
                artistCredits = Array.Empty<object>(),
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = Array.Empty<object>(),
                ownedCopy = (object?)null
            });
        using JsonDocument missingArtistDocument = await ReadJsonAsync(missingArtistResponse);

        Assert.Equal(HttpStatusCode.BadRequest, emptyTitleResponse.StatusCode);
        Assert.Equal("release.title_required", emptyTitleDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missingArtistResponse.StatusCode);
        Assert.Equal("release.artist_required", missingArtistDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Release entry create reuses new artists within the same request")]
    public async Task Release_entry_create_reuses_new_artists_within_the_same_request()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Shared Artist Draft",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new object[] { new { name = "Shared Artist", role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Shared Artist Track",
                        position = 1,
                        durationSeconds = 297,
                        artistCredits = new object[] { new { name = "Shared Artist", role = "mainArtist" } },
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid releaseArtistId = createDocument.RootElement.GetProperty("artistCredits")[0].GetProperty("artistId").GetGuid();
        Guid trackArtistId = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("artistCredits")[0].GetProperty("artistId").GetGuid();
        Assert.Equal(releaseArtistId, trackArtistId);

        using HttpResponseMessage artistsResponse = await client.GetAsync("/api/artists?search=Shared%20Artist&limit=10&offset=0");
        using JsonDocument artistsDocument = await ReadJsonAsync(artistsResponse);

        Assert.Equal(HttpStatusCode.OK, artistsResponse.StatusCode);
        Assert.Equal(1, artistsDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Shared Artist", artistsDocument.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name = "Factory" });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, string type = "person")
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type, name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        string content = await response.Content.ReadAsStringAsync();

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"Response was not JSON. Status: {response.StatusCode}. Body: {content}", exception);
        }
    }
}
