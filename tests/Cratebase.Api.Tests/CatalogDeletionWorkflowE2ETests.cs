using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class CatalogDeletionWorkflowE2ETests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CatalogDeletionWorkflowE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Deleting a release removes owned copies credits and unused linked tracks")]
    public async Task Deleting_a_release_removes_owned_copies_credits_and_unused_linked_tracks()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Delete Cascade EP",
                type = "ep",
                isVariousArtists = false,
                artistCredits = new object[] { new { name = "Delete Cascade Artist", role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Delete Cascade Track",
                        position = 1,
                        durationSeconds = 180,
                        artistCredits = Array.Empty<object>(),
                        versionNote = (string?)null
                    }
                },
                ownedCopy = new
                {
                    status = "owned",
                    medium = new { type = "vinyl", description = "12-inch" },
                    condition = (string?)null,
                    storageLocation = (string?)null
                }
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid releaseId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid trackId = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/releases/{releaseId}");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"release:{releaseId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        using HttpResponseMessage getReleaseResponse = await client.GetAsync($"/api/releases/{releaseId}");
        using JsonDocument getReleaseDocument = await ReadJsonAsync(getReleaseResponse);
        using HttpResponseMessage getTrackResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument getTrackDocument = await ReadJsonAsync(getTrackResponse);
        using HttpResponseMessage releaseCreditsResponse = await client.GetAsync($"/api/credits?targetType=release&targetId={releaseId}&limit=10&offset=0");
        using JsonDocument releaseCreditsDocument = await ReadJsonAsync(releaseCreditsResponse);
        using HttpResponseMessage trackCreditsResponse = await client.GetAsync($"/api/credits?targetType=track&targetId={trackId}&limit=10&offset=0");
        using JsonDocument trackCreditsDocument = await ReadJsonAsync(trackCreditsResponse);
        using HttpResponseMessage ownedItemsResponse = await client.GetAsync("/api/owned-items?limit=10&offset=0");
        using JsonDocument ownedItemsDocument = await ReadJsonAsync(ownedItemsResponse);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getReleaseResponse.StatusCode);
        Assert.Equal("release.not_found", getReleaseDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, getTrackResponse.StatusCode);
        Assert.Equal("track.not_found", getTrackDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, releaseCreditsDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, trackCreditsDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, ownedItemsDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Deleting a track removes release links and track credits")]
    public async Task Deleting_a_track_removes_release_links_and_track_credits()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Delete Track Link EP",
                type = "ep",
                isVariousArtists = false,
                artistCredits = new object[] { new { name = "Delete Track Artist", role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "Delete Linked Track",
                        position = 1,
                        durationSeconds = 181,
                        artistCredits = new object[] { new { name = "Delete Track Artist", role = "mainArtist" } },
                        versionNote = (string?)null
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid releaseId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid trackId = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/tracks/{trackId}");
        deleteRequest.Headers.Add("X-Cratebase-Confirm-Delete", $"track:{trackId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        using HttpResponseMessage getTrackResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument getTrackDocument = await ReadJsonAsync(getTrackResponse);
        using HttpResponseMessage getReleaseResponse = await client.GetAsync($"/api/releases/{releaseId}");
        using JsonDocument getReleaseDocument = await ReadJsonAsync(getReleaseResponse);
        using HttpResponseMessage trackCreditsResponse = await client.GetAsync($"/api/credits?targetType=track&targetId={trackId}&limit=10&offset=0");
        using JsonDocument trackCreditsDocument = await ReadJsonAsync(trackCreditsResponse);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getTrackResponse.StatusCode);
        Assert.Equal("track.not_found", getTrackDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, getReleaseResponse.StatusCode);
        Assert.Equal(0, getReleaseDocument.RootElement.GetProperty("tracklist").GetArrayLength());
        Assert.Equal(0, trackCreditsDocument.RootElement.GetProperty("total").GetInt32());
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
