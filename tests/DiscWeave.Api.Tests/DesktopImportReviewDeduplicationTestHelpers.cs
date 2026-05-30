using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed partial class DesktopImportReviewDeduplicationTests
{
    private const string BeginsContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string BlueTruthContentHash = "abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd";
    private static readonly string[] StevenJulien = ["Steven Julien"];

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

    private static async Task DeleteReleaseOwnedItemAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/owned-items?limit=10&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement releaseItem = document.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("targetType").GetString() == "release");
        Guid ownedItemId = releaseItem.GetProperty("id").GetGuid();
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/owned-items/{ownedItemId}");
        request.Headers.Add("X-DiscWeave-Confirm-Delete", $"owned-item:{ownedItemId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
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
