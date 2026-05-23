using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

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

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateManualPlaylistAsync(HttpClient client, string name, Guid releaseId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name,
                type = "manual",
                entries = new[] { new { kind = "release", id = releaseId } }
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
