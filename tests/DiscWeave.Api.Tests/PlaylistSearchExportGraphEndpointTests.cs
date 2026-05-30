using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class PlaylistSearchExportGraphEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public PlaylistSearchExportGraphEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Playlists appear in search documents and catalog graph backlinks")]
    public async Task Playlists_appear_in_search_documents_and_catalog_graph_backlinks()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Backlinked Release");
        Guid playlistId = await CreateManualPlaylistAsync(client, "Archive routes", releaseId);

        using HttpResponseMessage searchResponse = await client.GetAsync("/api/search?query=Archive%20routes&entityType=playlist&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
        using JsonDocument searchDocument = await ReadJsonAsync(searchResponse);
        JsonElement searchItem = Assert.Single(searchDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("playlist", searchItem.GetProperty("type").GetString());
        Assert.Equal(playlistId, searchItem.GetProperty("id").GetGuid());

        using HttpResponseMessage graphResponse = await client.GetAsync($"/api/catalog-graph/release/{releaseId}");
        Assert.Equal(HttpStatusCode.OK, graphResponse.StatusCode);
        using JsonDocument graphDocument = await ReadJsonAsync(graphResponse);
        JsonElement playlistLink = Assert.Single(graphDocument.RootElement.GetProperty("sections").GetProperty("playlists").EnumerateArray());
        Assert.Equal(playlistId, playlistLink.GetProperty("id").GetGuid());
        Assert.Equal("playlist", playlistLink.GetProperty("type").GetString());
    }

    [Fact(DisplayName = "Catalog graph context describes playlist entries and owned coverage")]
    public async Task Catalog_graph_context_describes_playlist_entries_and_owned_coverage()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Playlist Release");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "owned", "vinyl");
        Guid playlistId = await CreateManualPlaylistAsync(client, "Archive routes", releaseId);

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/playlist/{playlistId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("playlist", document.RootElement.GetProperty("entity").GetProperty("type").GetString());
        Assert.Equal(playlistId, document.RootElement.GetProperty("entity").GetProperty("id").GetGuid());
        Assert.Equal("Archive routes", document.RootElement.GetProperty("entity").GetProperty("title").GetString());
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains(document.RootElement.GetProperty("sections").GetProperty("ownedCopies").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
        Assert.Contains(document.RootElement.GetProperty("collectorSignals").EnumerateArray(), signal => signal.GetString() == "vinyl");
    }

    [Fact(DisplayName = "Catalog graph context deduplicates playlist labels and links track entries")]
    public async Task Catalog_graph_context_deduplicates_playlist_labels_and_links_track_entries()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(client, "Shared Label");
        Guid firstReleaseId = await CreateReleaseAsync(client, "First Shared Release", labelId);
        Guid secondReleaseId = await CreateReleaseAsync(client, "Second Shared Release", labelId);
        Guid trackId = await CreateTrackAsync(client, "Loose Track");
        Guid playlistId = await CreateManualPlaylistAsync(
            client,
            "Shared label set",
            ("release", firstReleaseId),
            ("release", secondReleaseId),
            ("track", trackId));

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/playlist/{playlistId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement sections = document.RootElement.GetProperty("sections");
        Assert.Contains(sections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == firstReleaseId);
        Assert.Contains(sections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == secondReleaseId);
        Assert.Contains(sections.GetProperty("tracks").EnumerateArray(), link => link.GetProperty("id").GetGuid() == trackId);
        JsonElement label = Assert.Single(sections.GetProperty("labels").EnumerateArray());
        Assert.Equal(labelId, label.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "Catalog graph context resolves smart playlist results")]
    public async Task Catalog_graph_context_resolves_smart_playlist_results()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        string[] tags = ["smart-graph"];
        Guid labelId = await CreateLabelAsync(client, "Smart Label");
        Guid releaseId = await CreateReleaseAsync(client, "Tagged Smart Release", labelId, tags);
        Guid trackId = await CreateTrackAsync(client, "Tagged Smart Track", tags);
        Guid trackAppearanceReleaseId = await CreateReleaseWithTrackAsync(client, "Tagged Smart Track Appearance", trackId, labelId);
        Guid ownedItemId = await CreateOwnedItemAsync(client, "track", trackId, "owned", "vinyl");
        Guid playlistId = await CreateSmartPlaylistAsync(client, "Tagged smart set", tags);

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/playlist/{playlistId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement sections = document.RootElement.GetProperty("sections");
        Assert.Contains(sections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains(sections.GetProperty("releases").EnumerateArray(), link => link.GetProperty("id").GetGuid() == trackAppearanceReleaseId);
        Assert.Contains(sections.GetProperty("tracks").EnumerateArray(), link => link.GetProperty("id").GetGuid() == trackId);
        Assert.Contains(sections.GetProperty("ownedCopies").EnumerateArray(), link => link.GetProperty("id").GetGuid() == ownedItemId);
        JsonElement label = Assert.Single(sections.GetProperty("labels").EnumerateArray());
        Assert.Equal(labelId, label.GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "Exports include playlist definitions and manual entries")]
    public async Task Exports_include_playlist_definitions_and_manual_entries()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Exported Release");
        Guid playlistId = await CreateManualPlaylistAsync(client, "Export set", releaseId);

        using HttpResponseMessage jsonResponse = await client.GetAsync("/api/exports/json");
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        using JsonDocument document = await ReadJsonAsync(jsonResponse);
        JsonElement playlist = Assert.Single(document.RootElement.GetProperty("playlists").EnumerateArray());
        Assert.Equal(playlistId, playlist.GetProperty("id").GetGuid());
        Assert.Equal("manual", playlist.GetProperty("type").GetString());
        JsonElement entry = Assert.Single(playlist.GetProperty("entries").EnumerateArray());
        Assert.Equal(releaseId, entry.GetProperty("id").GetGuid());

        using HttpResponseMessage csvResponse = await client.GetAsync("/api/exports/csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        await using Stream stream = await csvResponse.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        string playlistsCsv = await ReadEntryAsync(archive, "playlists.csv");
        string entriesCsv = await ReadEntryAsync(archive, "playlist_entries.csv");
        Assert.Contains($"{playlistId},Export set,manual", playlistsCsv);
        Assert.Contains($"{playlistId},0,release,{releaseId}", entriesCsv);
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(
        HttpClient client,
        string title,
        Guid? labelId = null,
        IReadOnlyList<string>? tags = null)
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
                tags = tags ?? EmptyStrings
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithTrackAsync(
        HttpClient client,
        string title,
        Guid trackId,
        Guid? labelId = null)
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(
        HttpClient client,
        string title,
        IReadOnlyList<string>? tags = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, genres = EmptyStrings, tags = tags ?? EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status, string medium)
    {
        return await CreateOwnedItemAsync(client, "release", releaseId, status, medium);
    }

    private static async Task<Guid> CreateOwnedItemAsync(
        HttpClient client,
        string targetType,
        Guid targetId,
        string status,
        string medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType,
                targetId,
                status,
                medium = new { type = medium, description = medium }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateManualPlaylistAsync(HttpClient client, string name, Guid releaseId)
    {
        return await CreateManualPlaylistAsync(client, name, ("release", releaseId));
    }

    private static async Task<Guid> CreateManualPlaylistAsync(HttpClient client, string name, params (string Kind, Guid Id)[] entries)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name,
                type = "manual",
                entries = entries.Select(entry => new { kind = entry.Kind, id = entry.Id }).ToArray()
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateSmartPlaylistAsync(HttpClient client, string name, IReadOnlyList<string> tags)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name,
                type = "smart",
                rules = new
                {
                    tags,
                    genres = EmptyStrings,
                    media = EmptyStrings,
                    ownershipStatuses = EmptyStrings
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

    private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
    {
        ZipArchiveEntry entry = archive.GetEntry(name)
            ?? throw new InvalidOperationException($"CSV export entry was not found: {name}");
        await using Stream stream = entry.Open();
        using var reader = new StreamReader(stream);

        return await reader.ReadToEndAsync();
    }
}
