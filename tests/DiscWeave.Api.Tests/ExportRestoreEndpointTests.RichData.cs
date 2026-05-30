using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed partial class ExportRestoreEndpointTests
{
    private static async Task<RichSnapshotIds> CreateRichSnapshotDataAsync(HttpClient client)
    {
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid artistId = await CreateArtistAsync(client, "Bernard Sumner");
        Guid groupArtistId = await CreateArtistAsync(client, "New Order", "group");
        (Guid releaseId, Guid trackId) = await CreateReleaseWithTrackIdsAsync(client, labelId, artistId);
        Guid remixTrackId = await CreateTrackAsync(client, "Age of Consent (Restored Mix)");
        _ = await CreateOwnedItemAsync(client, releaseId);
        _ = await CreateOwnedItemWithMediumAsync(
            client,
            releaseId,
            new { type = "digital", path = $"/music/{releaseId:N}.flac", format = "flac" });
        _ = await CreateOwnedItemWithMediumAsync(client, releaseId, new { type = "cd", discCount = 1 });
        _ = await CreateOwnedItemWithMediumAsync(client, releaseId, new { type = "cassette", description = "Tape" });
        _ = await CreateOwnedItemWithMediumAsync(client, releaseId, new { type = "other", description = "DAT safety copy" });
        Guid manualPlaylistId = await CreateManualPlaylistAsync(client, releaseId, trackId);
        Guid smartPlaylistId = await CreateSmartPlaylistAsync(client);
        _ = await CreateArtistRelationAsync(client, artistId, groupArtistId, "memberOf", 1980, 1993);
        _ = await CreateArtistRelationAsync(client, groupArtistId, artistId, "alias", null, null);
        _ = await CreateTrackRelationAsync(client, remixTrackId, trackId);
        Guid ratingCriterionId = await FindOverallCriterionIdAsync(client);
        Guid ratingId = await CreateRatingAsync(client, trackId, ratingCriterionId);
        _ = await CreateInactiveDictionaryEntryAsync(client);
        _ = await CreateInactiveRatingCriterionAsync(client);
        _ = await CreateInactiveImportPatternAsync(client);

        return new RichSnapshotIds(groupArtistId, manualPlaylistId, smartPlaylistId, ratingId);
    }

    private static async Task<Guid> CreateManualPlaylistAsync(HttpClient client, Guid releaseId, Guid trackId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name = "Restore sequence",
                description = "Manual sequence for restore coverage",
                type = "manual",
                entries = new object[]
                {
                    new { kind = "release", id = releaseId },
                    new { kind = "track", id = trackId }
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateSmartPlaylistAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new
            {
                name = "Factory post-punk vinyl",
                type = "smart",
                rules = new
                {
                    tags = FactoryTags,
                    genres = PostPunkGenres,
                    media = VinylMedia,
                    ownershipStatuses = OwnedStatuses,
                    yearFrom = 1980,
                    yearTo = 1985
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistRelationAsync(
        HttpClient client,
        Guid sourceArtistId,
        Guid targetArtistId,
        string type,
        int? startYear,
        int? endYear)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/artist-relations",
            new { sourceArtistId, targetArtistId, type, startYear, endYear });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackRelationAsync(HttpClient client, Guid sourceTrackId, Guid targetTrackId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/track-relations",
            new { sourceTrackId, targetTrackId, type = "remixOf" });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> FindOverallCriterionIdAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.GetAsync("/api/rating-criteria");
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "overall")
            .GetProperty("id")
            .GetGuid();
    }

    private static async Task<Guid> CreateRatingAsync(HttpClient client, Guid trackId, Guid criterionId)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/ratings/track/{trackId}/{criterionId}",
            new { value = 9 });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInactiveDictionaryEntryAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/settings/dictionaries",
            new
            {
                kind = "genre",
                code = "minimalSynth",
                name = "Minimal Synth",
                sortOrder = 900,
                isActive = false,
                mediaProfile = (string?)null
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInactiveRatingCriterionAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rating-criteria",
            new
            {
                code = "sleeve",
                name = "Sleeve",
                targetTypes = ReleaseTargetTypes,
                sortOrder = 90,
                isActive = false
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateInactiveImportPatternAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/settings/import-patterns",
            new
            {
                kind = "trackFile",
                template = "{artist}/{release}/{trackNumber} - {title}",
                sortOrder = 900,
                isActive = false
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }
}
