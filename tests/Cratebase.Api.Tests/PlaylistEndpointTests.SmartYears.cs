using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed partial class PlaylistEndpointTests
{
    [Fact(DisplayName = "Smart playlists evaluate track years across all release appearances")]
    public async Task Smart_playlists_evaluate_track_years_across_all_release_appearances()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Autechre");
        using JsonDocument originalRelease = await CreateReleaseWithTracklistAsync(
            client,
            "Original press",
            artistId,
            [
                new
                {
                    title = "Shared Surface",
                    position = 1,
                    durationSeconds = (int?)null,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                }
            ],
            year: 1988);
        Guid trackId = originalRelease.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();
        using JsonDocument laterRelease = await CreateReleaseWithTracklistAsync(
            client,
            "Later compilation",
            artistId,
            [
                new
                {
                    trackId,
                    position = 1,
                    versionNote = "Compilation appearance"
                }
            ],
            year: 2001);
        _ = laterRelease.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name = "2000s appearances",
                type = "smart",
                rules = new
                {
                    yearFrom = 2000,
                    yearTo = 2001
                }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] results = [.. document.RootElement.GetProperty("results").EnumerateArray()];
        Assert.Contains(results, result =>
            result.GetProperty("kind").GetString() == "track" &&
            result.GetProperty("id").GetGuid() == trackId);
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> CreateReleaseWithTracklistAsync(
        HttpClient client,
        string title,
        Guid artistId,
        object[] tracklist,
        int year)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title,
                type = "album",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                year,
                genres = EmptyStrings,
                tags = EmptyStrings,
                tracklist,
                ownedCopy = (object?)null
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }
}
