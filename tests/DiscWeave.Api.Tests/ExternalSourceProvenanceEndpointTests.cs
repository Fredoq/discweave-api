using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class ExternalSourceProvenanceEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTimeOffset AppliedAt = new(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Artist create update and list round trip external sources without collection ids")]
    public async Task Artist_create_update_and_list_round_trip_external_sources_without_collection_ids()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/artists",
            new
            {
                type = "group",
                name = "New Order",
                externalSources = new[] { Source("artist", "5876") }
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid artistId = createDocument.RootElement.GetProperty("id").GetGuid();
        Assert.False(createDocument.RootElement.TryGetProperty("collectionId", out _));
        AssertSource(createDocument.RootElement.GetProperty("externalSources")[0], "artist", "5876");

        using HttpResponseMessage preserveResponse = await client.PutAsJsonAsync(
            $"/api/artists/{artistId}",
            new { name = "  New Order Updated  " });
        using JsonDocument preserveDocument = await ReadJsonAsync(preserveResponse);

        using HttpResponseMessage clearResponse = await client.PutAsJsonAsync(
            $"/api/artists/{artistId}",
            new
            {
                name = "New Order",
                externalSources = Array.Empty<object>()
            });
        using JsonDocument clearDocument = await ReadJsonAsync(clearResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/artists?search=New%20Order&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, preserveResponse.StatusCode);
        _ = Assert.Single(preserveDocument.RootElement.GetProperty("externalSources").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.Equal(0, clearDocument.RootElement.GetProperty("externalSources").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        JsonElement listedArtist = listDocument.RootElement.GetProperty("items")[0];
        Assert.False(listedArtist.TryGetProperty("collectionId", out _));
        Assert.Equal(0, listedArtist.GetProperty("externalSources").GetArrayLength());
    }

    [Fact(DisplayName = "Release create stores release and new track external sources")]
    public async Task Release_create_stores_release_and_new_track_external_sources()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage releaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Blue Monday",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new object[] { new { name = "New Order", role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                year = 1983,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                externalSources = new[] { Source("release", "249504") },
                tracklist = new object[]
                {
                    new
                    {
                        title = "Blue Monday",
                        position = 1,
                        durationSeconds = 449,
                        artistCredits = Array.Empty<object>(),
                        versionNote = (string?)null,
                        externalSources = new[] { Source("track", "249504-A") }
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument releaseDocument = await ReadJsonAsync(releaseResponse);

        Assert.Equal(HttpStatusCode.Created, releaseResponse.StatusCode);
        AssertSource(releaseDocument.RootElement.GetProperty("externalSources")[0], "release", "249504");
        Guid trackId = releaseDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();

        using HttpResponseMessage trackResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument trackDocument = await ReadJsonAsync(trackResponse);

        Assert.Equal(HttpStatusCode.OK, trackResponse.StatusCode);
        AssertSource(trackDocument.RootElement.GetProperty("externalSources")[0], "track", "249504-A");
        Assert.False(trackDocument.RootElement.TryGetProperty("collectionId", out _));
    }

    [Fact(DisplayName = "Release create rejects inline external sources for existing track ids")]
    public async Task Release_create_rejects_inline_external_sources_for_existing_track_ids()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid trackId = await CreateTrackAsync(client, "Blue Monday");

        using HttpResponseMessage releaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Blue Monday",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new object[] { new { name = "New Order", role = "mainArtist" } },
                labels = Array.Empty<object>(),
                notOnLabel = true,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                tracklist = new object[]
                {
                    new
                    {
                        trackId,
                        position = 1,
                        versionNote = (string?)null,
                        externalSources = new[] { Source("track", "249504-A") }
                    }
                },
                ownedCopy = (object?)null
            });
        using JsonDocument document = await ReadJsonAsync(releaseResponse);

        Assert.Equal(HttpStatusCode.BadRequest, releaseResponse.StatusCode);
        Assert.Equal("release_track.external_sources_shape_invalid", document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Track create and update preserve replace and clear external sources")]
    public async Task Track_create_and_update_preserve_replace_and_clear_external_sources()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/tracks",
            new
            {
                title = "Blue Monday",
                durationSeconds = 449,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                credits = Array.Empty<object>(),
                releaseAppearances = Array.Empty<object>(),
                externalSources = new[] { Source("track", "249504-A") }
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid trackId = createDocument.RootElement.GetProperty("id").GetGuid();
        AssertSource(createDocument.RootElement.GetProperty("externalSources")[0], "track", "249504-A");

        using HttpResponseMessage preserveResponse = await client.PutAsJsonAsync(
            $"/api/tracks/{trackId}",
            new
            {
                title = "Blue Monday Updated",
                durationSeconds = 449,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                credits = Array.Empty<object>(),
                releaseAppearances = Array.Empty<object>()
            });
        using JsonDocument preserveDocument = await ReadJsonAsync(preserveResponse);

        using HttpResponseMessage clearResponse = await client.PutAsJsonAsync(
            $"/api/tracks/{trackId}",
            new
            {
                title = "Blue Monday",
                durationSeconds = 449,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                credits = Array.Empty<object>(),
                releaseAppearances = Array.Empty<object>(),
                externalSources = Array.Empty<object>()
            });
        using JsonDocument clearDocument = await ReadJsonAsync(clearResponse);

        Assert.Equal(HttpStatusCode.OK, preserveResponse.StatusCode);
        _ = Assert.Single(preserveDocument.RootElement.GetProperty("externalSources").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.Equal(0, clearDocument.RootElement.GetProperty("externalSources").GetArrayLength());
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new
            {
                title,
                durationSeconds = (int?)null,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                credits = Array.Empty<object>(),
                releaseAppearances = Array.Empty<object>()
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static object Source(string resourceType, string externalId)
    {
        return new
        {
            providerName = "discogs",
            resourceType,
            externalId,
            sourceUrl = $"https://www.discogs.com/{resourceType}/{externalId}",
            appliedAt = AppliedAt
        };
    }

    private static void AssertSource(JsonElement source, string resourceType, string externalId)
    {
        Assert.Equal("discogs", source.GetProperty("providerName").GetString());
        Assert.Equal(resourceType, source.GetProperty("resourceType").GetString());
        Assert.Equal(externalId, source.GetProperty("externalId").GetString());
        Assert.Equal($"https://www.discogs.com/{resourceType}/{externalId}", source.GetProperty("sourceUrl").GetString());
        Assert.Equal(AppliedAt, source.GetProperty("appliedAt").GetDateTimeOffset());
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
