using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DiscWeave.Api.Tests;

public sealed partial class ExportRestoreEndpointTests
{
    private static readonly DateTimeOffset ExternalSourceAppliedAt = new(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "JSON export and restore round trip external source provenance")]
    public async Task Json_export_and_restore_round_trip_external_source_provenance()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(adminClient, "Factory Records");
        Guid artistId = await CreateArtistWithExternalSourceAsync(adminClient);
        (Guid releaseId, Guid trackId) = await CreateReleaseWithExternalSourcesAsync(adminClient, labelId, artistId);

        string snapshot = await ExportJsonAsync(adminClient);
        using var exportDocument = JsonDocument.Parse(snapshot);
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot);
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        string restoredSnapshot = await ExportJsonAsync(userClient);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        Assert.DoesNotContain("collectionId", snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("collectionId", restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data provided by Discogs", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("token", snapshot, StringComparison.OrdinalIgnoreCase);
        AssertSource(exportDocument.RootElement.GetProperty("artists")[0].GetProperty("externalSources")[0], "artist", "5876");
        AssertSource(exportDocument.RootElement.GetProperty("releases")[0].GetProperty("externalSources")[0], "release", "249504");
        AssertSource(exportDocument.RootElement.GetProperty("tracks")[0].GetProperty("externalSources")[0], "track", "249504-A");
        Assert.Contains(artistId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(releaseId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(trackId.ToString(), restoredSnapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"externalId\":\"249504-A\"", restoredSnapshot, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "JSON restore treats missing external source arrays as empty")]
    public async Task Json_restore_treats_missing_external_source_arrays_as_empty()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient adminClient = await host.CreateAuthenticatedClientAsync();
        JsonObject snapshot = JsonNode.Parse(await CreateSnapshotAsync(adminClient))!.AsObject();
        RemoveExternalSources(snapshot["artists"]!.AsArray());
        RemoveExternalSources(snapshot["releases"]!.AsArray());
        RemoveExternalSources(snapshot["tracks"]!.AsArray());
        HttpClient userClient = await CreateUserClientAsync(host, adminClient);

        using HttpResponseMessage restoreResponse = await PostRestoreAsync(userClient, snapshot.ToJsonString());
        string restoreJson = await restoreResponse.Content.ReadAsStringAsync();
        string restoredSnapshot = await ExportJsonAsync(userClient);
        using var restoredDocument = JsonDocument.Parse(restoredSnapshot);

        Assert.True(restoreResponse.StatusCode == HttpStatusCode.OK, restoreJson);
        Assert.All(restoredDocument.RootElement.GetProperty("artists").EnumerateArray(), artist =>
            Assert.Equal(0, artist.GetProperty("externalSources").GetArrayLength()));
        Assert.All(restoredDocument.RootElement.GetProperty("releases").EnumerateArray(), release =>
            Assert.Equal(0, release.GetProperty("externalSources").GetArrayLength()));
        Assert.All(restoredDocument.RootElement.GetProperty("tracks").EnumerateArray(), track =>
            Assert.Equal(0, track.GetProperty("externalSources").GetArrayLength()));
    }

    private static async Task<Guid> CreateArtistWithExternalSourceAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artists",
            new
            {
                type = "group",
                name = "New Order",
                externalSources = new[] { Source("artist", "5876") }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(Guid ReleaseId, Guid TrackId)> CreateReleaseWithExternalSourcesAsync(
        HttpClient client,
        Guid labelId,
        Guid artistId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Blue Monday",
                type = "standalone",
                isVariousArtists = false,
                labelId,
                year = 1983,
                genres = Array.Empty<string>(),
                tags = Array.Empty<string>(),
                artistCredits = new[] { new { artistId, role = "mainArtist" } },
                externalSources = new[] { Source("release", "249504") },
                tracklist = new object[]
                {
                    new
                    {
                        title = "Blue Monday",
                        position = 1,
                        durationSeconds = 449,
                        externalSources = new[] { Source("track", "249504-A") }
                    }
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (
            document.RootElement.GetProperty("id").GetGuid(),
            document.RootElement.GetProperty("tracklist")[0].GetProperty("trackId").GetGuid());
    }

    private static object Source(string resourceType, string externalId)
    {
        return new
        {
            providerName = "discogs",
            resourceType,
            externalId,
            sourceUrl = $"https://www.discogs.com/{resourceType}/{externalId}",
            appliedAt = ExternalSourceAppliedAt
        };
    }

    private static void AssertSource(JsonElement source, string resourceType, string externalId)
    {
        Assert.Equal("discogs", source.GetProperty("providerName").GetString());
        Assert.Equal(resourceType, source.GetProperty("resourceType").GetString());
        Assert.Equal(externalId, source.GetProperty("externalId").GetString());
        Assert.Equal($"https://www.discogs.com/{resourceType}/{externalId}", source.GetProperty("sourceUrl").GetString());
        Assert.Equal(ExternalSourceAppliedAt, source.GetProperty("appliedAt").GetDateTimeOffset());
    }

    private static void RemoveExternalSources(JsonArray items)
    {
        foreach (JsonNode? item in items)
        {
            _ = item?.AsObject().Remove("externalSources");
        }
    }
}
