using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class ReleaseTracklistValidationE2ETests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ElectronicGenres = ["Electronic"];
    private readonly PostgresFixture _postgres;

    public ReleaseTracklistValidationE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Release entry create rejects duplicate existing tracks in one tracklist")]
    public async Task Release_entry_create_rejects_duplicate_existing_tracks_in_one_tracklist()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Autechre");
        Guid existingTrackId = await CreateSourceTrackAsync(client, artistId, "Dael");

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/releases",
            ReleasePayload(
                "Duplicate Tracklist",
                artistId,
                [
                    new { trackId = existingTrackId, position = 1, versionNote = (string?)null },
                    new { trackId = existingTrackId, position = 2, versionNote = "Duplicate" }
                ],
                type: "standalone",
                year: 1996));
        using JsonDocument duplicateDocument = await ReadJsonAsync(duplicateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
        Assert.Equal("release_track.track_duplicate", duplicateDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Release entry create rejects canonical track fields with existing track id")]
    public async Task Release_entry_create_rejects_canonical_track_fields_with_existing_track_id()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Fred again..");
        Guid existingTrackId = await CreateSourceTrackAsync(client, artistId, "Leavemealone");

        using HttpResponseMessage invalidShapeResponse = await client.PostAsJsonAsync(
            "/api/releases",
            ReleasePayload(
                "Invalid Existing Track Shape",
                artistId,
                [
                    new
                    {
                        trackId = existingTrackId,
                        title = "Should Not Mutate",
                        position = 1,
                        durationSeconds = 180,
                        artistCredits = new object[] { new { artistId, role = "mainArtist" } },
                        versionNote = (string?)null
                    }
                ],
                type: "standalone",
                year: 2026));
        using JsonDocument invalidShapeDocument = await ReadJsonAsync(invalidShapeResponse);

        Assert.Equal(HttpStatusCode.BadRequest, invalidShapeResponse.StatusCode);
        Assert.Equal("release_track.shape_invalid", invalidShapeDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Release entry create rejects duplicate global positions across discs")]
    public async Task Release_entry_create_rejects_duplicate_global_positions_across_discs()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid artistId = await CreateArtistAsync(client, "Autechre");

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/releases",
            ReleasePayload(
                "Duplicate Global Positions",
                artistId,
                [
                    new { title = "First Disc Track", position = 1, disc = "CD 1", side = (string?)null },
                    new { title = "Second Disc Track", position = 1, disc = "CD 2", side = (string?)null }
                ],
                type: "album",
                year: 1995));
        using JsonDocument duplicateDocument = await ReadJsonAsync(duplicateResponse);

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
        Assert.Equal("release_track.position_duplicate", duplicateDocument.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateSourceTrackAsync(
        HttpClient client,
        Guid artistId,
        string title,
        CancellationToken cancellationToken = default)
    {
        using JsonDocument sourceDocument = await CreateReleaseAsync(
            client,
            $"Source {title}",
            artistId,
            [
                new
                {
                    title,
                    position = 1,
                    durationSeconds = 222,
                    artistCredits = Array.Empty<object>(),
                    versionNote = (string?)null
                }
            ],
            cancellationToken);

        return sourceDocument.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid();
    }

    private static async Task<JsonDocument> CreateReleaseAsync(
        HttpClient client,
        string title,
        Guid artistId,
        object[] tracklist,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            ReleasePayload(title, artistId, tracklist),
            cancellationToken);
        JsonDocument document = await ReadJsonAsync(response, cancellationToken);
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

    private static async Task<Guid> CreateArtistAsync(
        HttpClient client,
        string name,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new { type = "person", name },
            cancellationToken);
        using JsonDocument document = await ReadJsonAsync(response, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken);

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
