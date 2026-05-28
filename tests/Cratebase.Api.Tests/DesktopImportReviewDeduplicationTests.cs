using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class DesktopImportReviewDeduplicationTests : IClassFixture<PostgresFixture>
{
    private const string BeginsContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string BlueTruthContentHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
    private static readonly string[] StevenJulien = ["Steven Julien"];

    private readonly PostgresFixture _postgres;

    public DesktopImportReviewDeduplicationTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Confirming the final open desktop import draft completes the session")]
    public async Task Confirming_the_final_open_desktop_import_draft_completes_the_session()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        using JsonDocument scan = await PostScanAsync(
            client,
            "/music/source",
            AudioFile(
                "/music/source",
                "/music/source/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash));

        using JsonDocument confirmation = await ConfirmOnlyDraftAsync(client, scan);

        Assert.Equal("completed", confirmation.RootElement.GetProperty("status").GetString());
        Assert.Equal("confirmed", confirmation.RootElement.GetProperty("drafts")[0].GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Skipping the final open desktop import draft completes the session")]
    public async Task Skipping_the_final_open_desktop_import_draft_completes_the_session()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        using JsonDocument scan = await PostScanAsync(
            client,
            "/music/source",
            AudioFile(
                "/music/source",
                "/music/source/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash));
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage skipResponse = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/skip", null);
        using JsonDocument skip = await ReadJsonAsync(skipResponse);

        Assert.Equal(HttpStatusCode.OK, skipResponse.StatusCode);
        Assert.Equal("completed", skip.RootElement.GetProperty("status").GetString());
        Assert.Equal("skipped", skip.RootElement.GetProperty("drafts")[0].GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Desktop import draft update rejects selected tracks outside the authenticated collection")]
    public async Task Desktop_import_draft_update_rejects_selected_tracks_outside_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        using JsonDocument adminScan = await PostScanAsync(
            adminClient,
            "/music/admin",
            AudioFile(
                "/music/admin",
                "/music/admin/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash));
        using JsonDocument adminConfirmation = await ConfirmOnlyDraftAsync(adminClient, adminScan);
        Guid adminTrackId = await SingleTrackIdAsync(adminClient, "Begins");
        using JsonDocument userScan = await PostScanAsync(
            userClient,
            "/music/user",
            AudioFile(
                "/music/user",
                "/music/user/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash));
        Guid userSessionId = userScan.RootElement.GetProperty("id").GetGuid();
        Guid userDraftId = userScan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await userClient.PutAsJsonAsync(
            $"/api/imports/{userSessionId}/drafts/{userDraftId}",
            DraftUpdatePayload(userScan, adminTrackId));
        using JsonDocument update = await ReadJsonAsync(updateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        Assert.Equal("release_import.selected_track_not_found", update.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Partial duplicate desktop import reuses the matching release and adds missing tracks")]
    public async Task Partial_duplicate_desktop_import_reuses_the_matching_release_and_adds_missing_tracks()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        using JsonDocument firstScan = await PostScanAsync(
            client,
            "/music/source",
            AudioFile(
                "/music/source",
                "/music/source/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash));
        using JsonDocument firstConfirmation = await ConfirmOnlyDraftAsync(client, firstScan);
        Guid existingTrackId = await SingleTrackIdAsync(client, "Begins");

        using JsonDocument duplicateScan = await PostScanAsync(
            client,
            "/music/expanded",
            AudioFile(
                "/music/expanded",
                "/music/expanded/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac",
                BeginsContentHash),
            AudioFile(
                "/music/expanded",
                "/music/expanded/[AA 01, 2016] Steven Julien - Fallen/02 Blue Truth.flac",
                BlueTruthContentHash));
        JsonElement duplicateTracks = duplicateScan.RootElement.GetProperty("drafts")[0].GetProperty("tracks");

        Assert.Equal(existingTrackId, duplicateTracks[0].GetProperty("selectedTrackId").GetGuid());
        Assert.Equal(JsonValueKind.Null, duplicateTracks[1].GetProperty("selectedTrackId").ValueKind);

        using JsonDocument duplicateConfirmation = await ConfirmOnlyDraftAsync(client, duplicateScan);

        using HttpResponseMessage releaseResponse = await client.GetAsync("/api/releases?search=Fallen&limit=10&offset=0");
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);
        JsonElement release = Assert.Single(releaseDocument.RootElement.GetProperty("items").EnumerateArray());
        JsonElement tracklist = release.GetProperty("tracklist");

        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        Assert.Equal(1, releaseDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, tracklist.GetArrayLength());
        Assert.Equal(existingTrackId, tracklist[0].GetProperty("trackId").GetGuid());
        Assert.Equal("Begins", tracklist[0].GetProperty("title").GetString());
        Assert.Equal("Blue Truth", tracklist[1].GetProperty("title").GetString());
        await AssertListTotalAsync(client, "/api/tracks?search=Begins&limit=10&offset=0", 1);
        await AssertListTotalAsync(client, "/api/tracks?search=Blue%20Truth&limit=10&offset=0", 1);
        await AssertListTotalAsync(client, "/api/owned-items?limit=10&offset=0", 2);
    }

    private static object AudioFile(string rootPath, string filePath, string contentHash)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        int? trackNumber = int.TryParse(fileName.Split(' ', 2)[0], out int parsedTrackNumber)
            ? parsedTrackNumber
            : null;
        string? title = trackNumber is null ? null : fileName.Split(' ', 2).ElementAtOrDefault(1);

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
                title,
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

    private static async Task<JsonDocument> ConfirmOnlyDraftAsync(HttpClient client, JsonDocument scan)
    {
        Guid sessionId = scan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = scan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();
        using HttpResponseMessage response = await client.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static object DraftUpdatePayload(JsonDocument scan, Guid selectedTrackId)
    {
        JsonElement draft = scan.RootElement.GetProperty("drafts")[0];
        JsonElement track = draft.GetProperty("tracks")[0];

        return new
        {
            title = draft.GetProperty("title").GetString(),
            type = draft.GetProperty("type").GetString(),
            catalogNumber = (string?)null,
            labelName = (string?)null,
            releaseDate = (string?)null,
            year = (int?)2016,
            isVariousArtists = false,
            notOnLabel = true,
            artistNames = StevenJulien,
            artistCredits = Array.Empty<object>(),
            labels = Array.Empty<object>(),
            selectedArtistIds = Array.Empty<Guid>(),
            genres = Array.Empty<string>(),
            tags = Array.Empty<string>(),
            coverPath = (string?)null,
            tracks = new object[]
            {
                new
                {
                    id = track.GetProperty("id").GetGuid(),
                    position = (int?)1,
                    title = track.GetProperty("title").GetString(),
                    durationSeconds = (int?)null,
                    artistNames = StevenJulien,
                    artistCredits = Array.Empty<object>(),
                    selectedArtistIds = Array.Empty<Guid>(),
                    selectedTrackId = (Guid?)selectedTrackId,
                    isSkipped = false
                }
            }
        };
    }

    private static async Task<Guid> SingleTrackIdAsync(HttpClient client, string search)
    {
        using HttpResponseMessage response = await client.GetAsync($"/api/tracks?search={Uri.EscapeDataString(search)}&limit=10&offset=0");
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

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync("/api/auth/register", new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync("/api/auth/login", new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password);
}
