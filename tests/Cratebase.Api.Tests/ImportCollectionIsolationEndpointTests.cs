using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ImportCollectionIsolationEndpointTests : IClassFixture<PostgresFixture>
{
    private const string ContentHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
    private static readonly string[] StevenJulien = ["Steven Julien"];
    private readonly PostgresFixture _postgres;

    public ImportCollectionIsolationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Import sessions and draft actions are scoped to the authenticated collection")]
    public async Task Import_sessions_and_draft_actions_are_scoped_to_the_authenticated_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        using JsonDocument adminScan = await PostScanAsync(adminClient, "/music/admin", "/music/admin/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac");
        Guid sessionId = adminScan.RootElement.GetProperty("id").GetGuid();
        Guid draftId = adminScan.RootElement.GetProperty("drafts")[0].GetProperty("id").GetGuid();
        Guid trackId = adminScan.RootElement.GetProperty("drafts")[0].GetProperty("tracks")[0].GetProperty("id").GetGuid();

        using HttpResponseMessage listResponse = await userClient.GetAsync("/api/imports");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        using HttpResponseMessage getResponse = await userClient.GetAsync($"/api/imports/{sessionId}");
        using HttpResponseMessage updateResponse = await userClient.PutAsJsonAsync(
            $"/api/imports/{sessionId}/drafts/{draftId}",
            DraftUpdatePayload(trackId));
        using HttpResponseMessage confirmResponse = await userClient.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/confirm", null);
        using HttpResponseMessage skipResponse = await userClient.PostAsync($"/api/imports/{sessionId}/drafts/{draftId}/skip", null);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(0, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, confirmResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, skipResponse.StatusCode);
    }

    [Fact(DisplayName = "Desktop import content hash deduplication is scoped per collection")]
    public async Task Desktop_import_content_hash_deduplication_is_scoped_per_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        using JsonDocument adminScan = await PostScanAsync(adminClient, "/music/admin", "/music/admin/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac");
        await ConfirmOnlyDraftAsync(adminClient, adminScan);

        using JsonDocument userScan = await PostScanAsync(userClient, "/music/user", "/music/user/[AA 01, 2016] Steven Julien - Fallen/01 Begins.flac");
        JsonElement userTrack = userScan.RootElement.GetProperty("drafts")[0].GetProperty("tracks")[0];
        await ConfirmOnlyDraftAsync(userClient, userScan);

        Assert.Equal(JsonValueKind.Null, userTrack.GetProperty("selectedTrackId").ValueKind);
        Assert.DoesNotContain(
            userTrack.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("code").GetString() == "release_import.duplicate_file");
        await AssertListTotalAsync(adminClient, "/api/tracks?search=Begins&limit=10&offset=0", 1);
        await AssertListTotalAsync(userClient, "/api/tracks?search=Begins&limit=10&offset=0", 1);
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

    private static async Task AssertListTotalAsync(HttpClient client, string route, int expected)
    {
        using HttpResponseMessage response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(expected, document.RootElement.GetProperty("total").GetInt32());
    }

    private static object DraftUpdatePayload(Guid trackId)
    {
        return new
        {
            title = "Edited from another collection",
            type = "unknown",
            isVariousArtists = false,
            notOnLabel = false,
            tracks = new[] { new { id = trackId, title = "Begins", isSkipped = false } }
        };
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password);
}
