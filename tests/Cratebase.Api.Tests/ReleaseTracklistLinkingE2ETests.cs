using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ReleaseTracklistLinkingE2ETests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectronicGenres = ["Electronic"];
    private readonly PostgresFixture _postgres;

    public ReleaseTracklistLinkingE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Release entry create links an existing track without duplicating it")]
    public async Task Release_entry_create_links_an_existing_track_without_duplicating_it()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Fred again..");

        using JsonDocument usbDocument = await CreateReleaseAsync(
            client,
            "USB",
            artistId,
            [
                new
                {
                    title = "Leavemealone",
                    position = 4,
                    durationSeconds = 222,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                }
            ]);
        Guid existingTrackId = usbDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using JsonDocument linkedReleaseDocument = await CreateReleaseAsync(
            client,
            "Shared Appearance",
            artistId,
            [
                new
                {
                    trackId = existingTrackId,
                    position = 1,
                    versionNote = "Shared release appearance"
                }
            ],
            type: "standalone",
            year: 2026);
        Guid linkedReleaseId = linkedReleaseDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage trackResponse = await client.GetAsync($"/api/tracks/{existingTrackId}");
        using JsonDocument trackDocument = await ReadJsonAsync(trackResponse);
        using HttpResponseMessage listResponse = await client.GetAsync("/api/tracks?search=Leavemealone&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(existingTrackId, linkedReleaseDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid());
        Assert.Equal("Leavemealone", linkedReleaseDocument.RootElement.GetProperty("tracklist")[0].GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, trackResponse.StatusCode);
        JsonElement appearances = trackDocument.RootElement.GetProperty("releaseAppearances");
        Assert.Equal(2, appearances.GetArrayLength());
        Assert.Contains(appearances.EnumerateArray(), appearance => appearance.GetProperty("releaseTitle").GetString() == "USB");
        Assert.Contains(appearances.EnumerateArray(), appearance => appearance.GetProperty("releaseId").GetGuid() == linkedReleaseId);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Release entry update replaces tracklist by unlinking only removed rows")]
    public async Task Release_entry_update_replaces_tracklist_by_unlinking_only_removed_rows()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Locked Club");

        using JsonDocument createDocument = await CreateReleaseAsync(
            client,
            "It's My Rave",
            artistId,
            [
                new
                {
                    title = "It's My Rave",
                    position = 1,
                    durationSeconds = (int?)null,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                },
                new
                {
                    title = "Rabotaet",
                    position = 2,
                    durationSeconds = (int?)null,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                }
            ],
            type: "ep",
            year: 2026);
        Guid releaseId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid removedTrackId = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();
        Guid retainedTrackId = createDocument.RootElement.GetProperty("tracklist")[1].GetProperty("trackId").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            ReleasePayload(
                "It's My Rave",
                artistId,
                [
                    new
                    {
                        trackId = retainedTrackId,
                        position = 1,
                        versionNote = "Only retained appearance"
                    }
                ],
                type: "ep",
                year: 2026));
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage removedTrackResponse = await client.GetAsync($"/api/tracks/{removedTrackId}");
        using JsonDocument removedTrackDocument = await ReadJsonAsync(removedTrackResponse);
        using HttpResponseMessage retainedTrackResponse = await client.GetAsync($"/api/tracks/{retainedTrackId}");
        using JsonDocument retainedTrackDocument = await ReadJsonAsync(retainedTrackResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        JsonElement updatedTracklist = updateDocument.RootElement.GetProperty("tracklist");
        _ = Assert.Single(updatedTracklist.EnumerateArray());
        Assert.Equal(retainedTrackId, updatedTracklist[0].GetProperty("trackId").GetGuid());
        Assert.Equal(1, updatedTracklist[0].GetProperty("position").GetInt32());
        Assert.Equal(HttpStatusCode.OK, removedTrackResponse.StatusCode);
        Assert.Empty(removedTrackDocument.RootElement.GetProperty("releaseAppearances").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, retainedTrackResponse.StatusCode);
        _ = Assert.Single(retainedTrackDocument.RootElement.GetProperty("releaseAppearances").EnumerateArray());
    }

    [Fact(DisplayName = "Release entry update keeps tracklist when omitted")]
    public async Task Release_entry_update_keeps_tracklist_when_omitted()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Autechre");

        using JsonDocument createDocument = await CreateReleaseAsync(
            client,
            "Amber",
            artistId,
            [
                new
                {
                    title = "Foil",
                    position = 1,
                    durationSeconds = 398,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                }
            ],
            year: 1994);
        Guid releaseId = createDocument.RootElement.GetProperty("id").GetGuid();
        Guid trackId = createDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            new
            {
                title = "Amber Expanded",
                type = "album",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                year = 1994,
                genres = ElectronicGenres,
                tags = Array.Empty<string>(),
                ownedCopy = (object?)null
            });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        JsonElement tracklist = updateDocument.RootElement.GetProperty("tracklist");
        _ = Assert.Single(tracklist.EnumerateArray());
        Assert.Equal(trackId, tracklist[0].GetProperty("trackId").GetGuid());
        Assert.Equal("Foil", tracklist[0].GetProperty("title").GetString());
    }

    private static async Task<JsonDocument> CreateReleaseAsync(
        HttpClient client,
        string title,
        Guid artistId,
        object[] tracklist,
        string type = "album",
        int year = 2024)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            ReleasePayload(title, artistId, tracklist, type, year));
        JsonDocument document = await ReadJsonAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, document.RootElement.ToString());

        return document;
    }

    private static object ReleasePayload(
        string title,
        Guid artistId,
        object[]? tracklist,
        string type = "album",
        int year = 2024)
    {
        return new
        {
            title,
            type,
            isVariousArtists = false,
            artistCredits = new object[] { new { artistId, role = "mainArtist" } },
            labels = Array.Empty<object>(),
            notOnLabel = true,
            year,
            genres = ElectronicGenres,
            tags = Array.Empty<string>(),
            tracklist,
            ownedCopy = (object?)null
        };
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
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
