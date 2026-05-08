using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class TrackEndpointContractTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectronicGenres = ["Electronic"];
    private static readonly string[] ExpectedTrackArtists = ["Fred again..", "PARISI", "Eyelar"];
    private static readonly string[] UpdatedTags = ["updated"];
    private readonly PostgresFixture _postgres;

    public TrackEndpointContractTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Track responses include all track credits and release appearances")]
    public async Task Track_responses_include_all_track_credits_and_release_appearances()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid firstArtistId = await CreateArtistAsync(client, "Fred again..");
        Guid secondArtistId = await CreateArtistAsync(client, "PARISI");
        Guid thirdArtistId = await CreateArtistAsync(client, "Eyelar");
        Guid labelId = await CreateLabelAsync(client);

        using HttpResponseMessage releaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "This is Real (Disappear)",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new object[]
                {
                    new { artistId = firstArtistId, role = "mainArtist" },
                    new { artistId = secondArtistId, role = "mainArtist" },
                    new { artistId = thirdArtistId, role = "mainArtist" }
                },
                labels = new object[] { new { labelId, catalogNumber = (string?)null, hasNoCatalogNumber = true } },
                notOnLabel = false,
                year = 2026,
                genres = ElectronicGenres,
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        title = "This is Real (Disappear)",
                        position = 1,
                        durationSeconds = 297,
                        artistCredits = Array.Empty<object>(),
                        versionNote = "Single version"
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);
        Assert.True(releaseResponse.StatusCode == HttpStatusCode.Created, releaseDocument.RootElement.ToString());
        Guid releaseId = releaseDocument.RootElement.GetProperty("id").GetGuid();
        Guid trackId = releaseDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/tracks?search=real&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        JsonElement track = getDocument.RootElement;
        Assert.Equal(3, track.GetProperty("credits").GetArrayLength());
        string[] actualTrackArtists = [.. track.GetProperty("credits").EnumerateArray().Select(credit => credit.GetProperty("artistName").GetString() ?? string.Empty)];
        Assert.Equal(ExpectedTrackArtists, actualTrackArtists);
        Assert.All(track.GetProperty("credits").EnumerateArray(), credit => Assert.Equal("mainArtist", credit.GetProperty("role").GetString()));
        Assert.Equal(1, track.GetProperty("releaseAppearances").GetArrayLength());
        JsonElement appearance = track.GetProperty("releaseAppearances")[0];
        Assert.Equal(releaseId, appearance.GetProperty("releaseId").GetGuid());
        Assert.Equal("This is Real (Disappear)", appearance.GetProperty("releaseTitle").GetString());
        Assert.Equal("Fred again.., PARISI, Eyelar", appearance.GetProperty("releaseArtist").GetString());
        Assert.Equal("Factory", appearance.GetProperty("label").GetString());
        Assert.Equal(1, appearance.GetProperty("position").GetInt32());
        Assert.Equal(297, appearance.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("Single version", appearance.GetProperty("versionNote").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        JsonElement listedTrack = listDocument.RootElement.GetProperty("items")[0];
        Assert.Equal(3, listedTrack.GetProperty("credits").GetArrayLength());
        Assert.Equal(1, listedTrack.GetProperty("releaseAppearances").GetArrayLength());
    }

    [Fact(DisplayName = "Updating a track replaces its credits and appearances without deleting other release rows")]
    public async Task Updating_a_track_replaces_its_credits_and_appearances_without_deleting_other_release_rows()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Autechre");
        Guid producerId = await CreateArtistAsync(client, "The Designers Republic", "group");

        using HttpResponseMessage firstReleaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Tri Repetae",
                type = "album",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                year = 1995,
                genres = ElectronicGenres,
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new { title = "Dael", position = 1, durationSeconds = 398, artistCredits = Array.Empty<object>(), versionNote = (string?)null },
                    new { title = "Clipper", position = 2, durationSeconds = 522, artistCredits = Array.Empty<object>(), versionNote = (string?)null }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument firstReleaseDocument = await ReadJsonAsync(firstReleaseResponse);
        Assert.True(firstReleaseResponse.StatusCode == HttpStatusCode.Created, firstReleaseDocument.RootElement.ToString());
        Guid firstReleaseId = firstReleaseDocument.RootElement.GetProperty("id").GetGuid();
        Guid targetTrackId = firstReleaseDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();
        Guid untouchedTrackId = firstReleaseDocument.RootElement.GetProperty("tracklist")[1].GetProperty("trackId").GetGuid();

        using HttpResponseMessage secondReleaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Dael Versions",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                year = 1996,
                genres = ElectronicGenres,
                tags = Array.Empty<string>(),
                tracklist = Array.Empty<object>(),
                ownedCopy = (object?)null
            });
        using JsonDocument secondReleaseDocument = await ReadJsonAsync(secondReleaseResponse);
        Assert.True(secondReleaseResponse.StatusCode == HttpStatusCode.Created, secondReleaseDocument.RootElement.ToString());
        Guid secondReleaseId = secondReleaseDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/tracks/{targetTrackId}",
            new
            {
                title = "Dael",
                durationSeconds = 398,
                genres = ElectronicGenres,
                tags = UpdatedTags,
                credits = new object[] { new { artistId = producerId, role = "producer" } },
                releaseAppearances = new object[]
                {
                    new { releaseId = firstReleaseId, position = 1, versionNote = "Album version" },
                    new { releaseId = secondReleaseId, position = 3, versionNote = "Version archive" }
                }
            });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage firstReleaseGetResponse = await client.GetAsync($"/api/releases/{firstReleaseId}");
        using JsonDocument firstReleaseGetDocument = await ReadJsonAsync(firstReleaseGetResponse);

        using HttpResponseMessage secondReleaseGetResponse = await client.GetAsync($"/api/releases/{secondReleaseId}");
        using JsonDocument secondReleaseGetDocument = await ReadJsonAsync(secondReleaseGetResponse);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        JsonElement updatedTrack = updateDocument.RootElement;
        _ = Assert.Single(updatedTrack.GetProperty("credits").EnumerateArray());
        Assert.Equal("producer", updatedTrack.GetProperty("credits")[0].GetProperty("role").GetString());
        Assert.Equal(2, updatedTrack.GetProperty("releaseAppearances").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, firstReleaseGetResponse.StatusCode);
        JsonElement firstTracklist = firstReleaseGetDocument.RootElement.GetProperty("tracklist");
        Assert.Equal(2, firstTracklist.GetArrayLength());
        Assert.Contains(firstTracklist.EnumerateArray(), track => track.GetProperty("trackId").GetGuid() == untouchedTrackId);
        Assert.Equal(HttpStatusCode.OK, secondReleaseGetResponse.StatusCode);
        JsonElement secondTracklist = secondReleaseGetDocument.RootElement.GetProperty("tracklist");
        _ = Assert.Single(secondTracklist.EnumerateArray());
        Assert.Equal(targetTrackId, secondTracklist[0].GetProperty("trackId").GetGuid());
        Assert.Equal(3, secondTracklist[0].GetProperty("position").GetInt32());
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name, string type = "person")
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type, name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name = "Factory" });
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
