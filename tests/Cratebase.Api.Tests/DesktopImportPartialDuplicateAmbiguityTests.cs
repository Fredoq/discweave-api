using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class DesktopImportPartialDuplicateAmbiguityTests : IClassFixture<PostgresFixture>
{
    private const string BeginsContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string BlueTruthContentHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
    private const string DriftContentHash = "fedcbafedcbafedcbafedcbafedcbafedcbafedcbafedcbafedcbafedcba";
    private static readonly string[] StevenJulien = ["Steven Julien"];
    private readonly PostgresFixture _postgres;

    public DesktopImportPartialDuplicateAmbiguityTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Partial duplicate import does not merge unrelated selected tracks into a same-title release")]
    public async Task Partial_duplicate_import_does_not_merge_unrelated_selected_tracks_into_same_title_release()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        await ConfirmOnlyDraftAsync(client, await PostScanAsync(
            client,
            "/music/begins",
            AudioFile("/music/begins", "/music/begins/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac", BeginsContentHash)));
        await ConfirmOnlyDraftAsync(client, await PostScanAsync(
            client,
            "/music/blue-truth",
            AudioFile("/music/blue-truth", "/music/blue-truth/[AA 01, 2016] Steven Julien - Fallen/02 Blue Truth.flac", BlueTruthContentHash)));

        using JsonDocument expandedScan = await PostScanAsync(
            client,
            "/music/expanded",
            AudioFile("/music/expanded", "/music/expanded/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac", BeginsContentHash),
            AudioFile("/music/expanded", "/music/expanded/[AA 01, 2016] Steven Julien - Fallen/02 Blue Truth.flac", BlueTruthContentHash),
            AudioFile("/music/expanded", "/music/expanded/[AA 01, 2016] Steven Julien - Fallen/03 Drift.flac", DriftContentHash));

        JsonElement draftTracks = expandedScan.RootElement.GetProperty("drafts")[0].GetProperty("tracks");
        Assert.NotEqual(JsonValueKind.Null, draftTracks[0].GetProperty("selectedTrackId").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, draftTracks[1].GetProperty("selectedTrackId").ValueKind);
        Assert.Equal(JsonValueKind.Null, draftTracks[2].GetProperty("selectedTrackId").ValueKind);

        await ConfirmOnlyDraftAsync(client, expandedScan);

        using HttpResponseMessage releaseResponse = await client.GetAsync("/api/releases?search=Fallen&limit=10&offset=0");
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);
        int[] tracklistLengths =
        [
            .. releaseDocument.RootElement.GetProperty("items")
                .EnumerateArray()
                .Select(release => release.GetProperty("tracklist").GetArrayLength())
                .Order()
        ];

        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        Assert.Equal(3, releaseDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal([1, 1, 3], tracklistLengths);
    }

    private static object AudioFile(string rootPath, string filePath, string contentHash)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        int? trackNumber = int.TryParse(fileName.Split(' ', 2)[0], out int parsedTrackNumber)
            ? parsedTrackNumber
            : null;

        return new
        {
            filePath,
            relativePath = Path.GetRelativePath(rootPath, filePath),
            format = "flac",
            sizeBytes = 9,
            lastModifiedAt = DateTimeOffset.UtcNow,
            contentHash,
            audioMetadata = new
            {
                title = trackNumber is null ? null : fileName.Split(' ', 2).ElementAtOrDefault(1),
                artists = Array.Empty<string>(),
                albumTitle = (string?)null,
                albumArtists = StevenJulien,
                catalogNumber = (string?)null,
                releaseDate = "2016",
                year = (int?)2016,
                durationSeconds = (int?)null,
                trackNumber
            },
            coverArtifact = (object?)null
        };
    }

    private static async Task<JsonDocument> PostScanAsync(HttpClient client, string rootPath, params object[] files)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/imports/desktop-folder-scans",
            new
            {
                sourceRoot = rootPath,
                ignoredFileCount = 0,
                files
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

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
