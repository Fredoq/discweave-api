using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscWeave.Api.Tests;

public sealed partial class ExportRestoreEndpointTests
{
    private static async Task<string> CreateSnapshotAsync(HttpClient client)
    {
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "New Order");
        Guid releaseId = await CreateReleaseWithTrackAsync(client, labelId, artistId);
        _ = await CreateOwnedItemAsync(client, releaseId);

        return await ExportJsonAsync(client);
    }

    private static string AddStoredCoverMetadata(string snapshot)
    {
        JsonObject document = JsonNode.Parse(snapshot)!.AsObject();
        JsonObject release = document["releases"]!.AsArray()[0]!.AsObject();
        release["releaseDate"] = "1983-05-02";
        release["coverImage"] = new JsonObject
        {
            ["url"] = "covers/power-corruption-lies.jpg",
            ["contentType"] = "image/jpeg",
            ["originalFileName"] = "Power Corruption Lies.jpg",
            ["sizeBytes"] = 4096,
            ["sourceType"] = "localUpload"
        };

        return document.ToJsonString();
    }

    private static async Task<HttpClient> CreateUserClientAsync(ApiTestHost host, HttpClient adminClient)
    {
        string email = $"collector-{Guid.CreateVersion7()}@example.com";
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest(email, "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest(email, "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return userClient;
    }

    private static async Task<string> ExportJsonAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/exports/json");
        string json = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return json;
    }

    private static async Task<HttpResponseMessage> PostRestoreAsync(HttpClient client, string json, bool confirm = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/exports/json/restore")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        if (confirm)
        {
            request.Headers.Add(RestoreConfirmationHeader, RestoreConfirmationValue);
        }

        return await client.SendAsync(request);
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
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

    private static async Task<Guid> CreateReleaseWithTrackAsync(HttpClient client, Guid labelId, Guid artistId)
    {
        (Guid releaseId, _) = await CreateReleaseWithTrackIdsAsync(client, labelId, artistId);

        return releaseId;
    }

    private static async Task<(Guid ReleaseId, Guid TrackId)> CreateReleaseWithTrackIdsAsync(HttpClient client, Guid labelId, Guid artistId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Power, Corruption & Lies",
                type = "album",
                isVariousArtists = false,
                labelId,
                year = 1983,
                genres = PostPunkGenres,
                tags = FactoryTags,
                artistCredits = new[] { new { artistId, role = "mainArtist" } },
                tracklist = new[] { new { title = "Age of Consent", position = 1, durationSeconds = 316 } }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid());
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, durationSeconds = 280, genres = Array.Empty<string>(), tags = FactoryTags });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        return await CreateOwnedItemWithMediumAsync(
            client,
            releaseId,
            new { type = "vinyl", description = "LP" },
            "nearMint",
            "Shelf A");
    }

    private static async Task<Guid> CreateOwnedItemWithMediumAsync(
        HttpClient client,
        Guid releaseId,
        object medium,
        string? condition = null,
        string? storageLocation = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium,
                condition,
                storageLocation
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateNamingProfileAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/settings/naming-profiles",
            new
            {
                name,
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position} {title}",
                trackFileWithArtistTemplate = "{position} {trackArtists} - {title}",
                sortOrder = 50,
                isDefault = false,
                isActive = true
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task PutReleaseNamingOverrideAsync(HttpClient client, Guid releaseId, Guid profileId)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}/naming-override",
            new
            {
                namingProfileId = profileId,
                releaseFolderTemplate = "{releaseArtists} - {title} ({year}) [{source} {format} {bitDepth}]",
                trackFileTemplate = "{position} {title}",
                trackFileWithArtistTemplate = "{position} {trackArtists} - {title}",
                source = "WEB"
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);

    private sealed record RichSnapshotIds(Guid GroupArtistId, Guid ManualPlaylistId, Guid SmartPlaylistId, Guid RatingId);
}
