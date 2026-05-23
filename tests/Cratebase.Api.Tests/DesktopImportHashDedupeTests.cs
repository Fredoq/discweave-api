using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class DesktopImportHashDedupeTests : IClassFixture<PostgresFixture>
{
    private const string ContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly string[] StevenJulien = ["Steven Julien"];
    private readonly PostgresFixture _postgres;

    public DesktopImportHashDedupeTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Desktop import uses content hash to preselect moved duplicate files and confirm as no-op")]
    public async Task Desktop_import_uses_content_hash_to_preselect_moved_duplicate_files_and_confirm_as_noop()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument firstScan = await PostScanAsync(client, "/music/source", "/music/source/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac");
        await ConfirmOnlyDraftAsync(client, firstScan);
        Guid existingTrackId = await SingleTrackIdAsync(client);

        using JsonDocument duplicateScan = await PostScanAsync(client, "/music/moved", "/music/moved/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac");
        JsonElement duplicateTrack = duplicateScan.RootElement.GetProperty("drafts")[0].GetProperty("tracks")[0];
        Assert.Equal(existingTrackId, duplicateTrack.GetProperty("selectedTrackId").GetGuid());
        Assert.Contains(
            duplicateTrack.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "release_import.duplicate_file");

        await ConfirmOnlyDraftAsync(client, duplicateScan);

        await AssertListTotalAsync(client, "/api/releases?search=Fallen&limit=10&offset=0", 1);
        await AssertListTotalAsync(client, "/api/tracks?search=Begins&limit=10&offset=0", 1);
        await AssertListTotalAsync(client, "/api/owned-items?limit=10&offset=0", 1);
    }

    private static async Task<JsonDocument> PostScanAsync(HttpClient client, string rootPath, string audioPath)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = rootPath,
                ignoredFileCount = 0,
                files = new[]
                {
                    new
                    {
                        filePath = audioPath,
                        relativePath = Path.GetRelativePath(rootPath, audioPath),
                        format = "flac",
                        sizeBytes = 9,
                        lastModifiedAt = DateTimeOffset.UtcNow,
                        contentHash = ContentHash,
                        audioMetadata = new
                        {
                            title = (string?)null,
                            artists = Array.Empty<string>(),
                            albumTitle = (string?)null,
                            albumArtists = StevenJulien,
                            catalogNumber = (string?)null,
                            releaseDate = "2016",
                            year = (int?)2016,
                            durationSeconds = (int?)null,
                            trackNumber = (int?)1
                        },
                        coverArtifact = (object?)null
                    }
                }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static async Task ConfirmOnlyDraftAsync(HttpClient client, JsonDocument scan)
    {
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();
        using HttpResponseMessage response = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<Guid> SingleTrackIdAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/tracks?search=Begins&limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement track = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());

        return track.GetProperty("id").GetGuid();
    }

    private static async Task AssertListTotalAsync(HttpClient client, string route, int expected)
    {
        using HttpResponseMessage response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(expected, document.RootElement.GetProperty("total").GetInt32());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
