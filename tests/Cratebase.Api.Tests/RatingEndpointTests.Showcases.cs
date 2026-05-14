using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed partial class RatingEndpointTests
{
    [Fact(DisplayName = "Rating showcases rank top values and expose unrated targets")]
    public async Task Rating_showcases_rank_top_values_and_expose_unrated_targets()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid criterionId = await FindOverallCriterionIdAsync(client);
        Guid firstTrackId = await CreateTrackAsync(client, "Windowlicker");
        Guid secondTrackId = await CreateTrackAsync(client, "Rhubarb");
        Guid thirdTrackId = await CreateTrackAsync(client, "Alberto Balsalm");
        Guid unratedTrackId = await CreateTrackAsync(client, "Stone in Focus");

        using HttpResponseMessage firstRatingResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{firstTrackId}/{criterionId}",
            new { value = 8 });
        using HttpResponseMessage secondRatingResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{secondTrackId}/{criterionId}",
            new { value = 10 });
        using HttpResponseMessage thirdRatingResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{thirdTrackId}/{criterionId}",
            new { value = 8 });

        using HttpResponseMessage topResponse = await client.GetAsync(
            $"/api/rating-showcases?criterionId={criterionId}&targetType=track&mode=top&scope=collection&limit=10&offset=0");
        using JsonDocument topDocument = await ReadJsonAsync(topResponse);
        using HttpResponseMessage unratedResponse = await client.GetAsync(
            $"/api/rating-showcases?criterionId={criterionId}&targetType=track&mode=unrated&limit=10&offset=0");
        using JsonDocument unratedDocument = await ReadJsonAsync(unratedResponse);
        using HttpResponseMessage invalidScopeResponse = await client.GetAsync(
            $"/api/rating-showcases?criterionId={criterionId}&targetType=track&mode=top&scope=library&limit=10&offset=0");
        using JsonDocument invalidScopeDocument = await ReadJsonAsync(invalidScopeResponse);

        Assert.Equal(HttpStatusCode.OK, firstRatingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondRatingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, thirdRatingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, topResponse.StatusCode);
        JsonElement topItems = topDocument.RootElement.GetProperty("items");
        Assert.Equal(3, topDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(secondTrackId, topItems[0].GetProperty("targetId").GetGuid());
        Assert.Equal(10, topItems[0].GetProperty("value").GetInt32());
        Assert.Equal(thirdTrackId, topItems[1].GetProperty("targetId").GetGuid());
        Assert.Equal(firstTrackId, topItems[2].GetProperty("targetId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, unratedResponse.StatusCode);
        JsonElement unratedItems = unratedDocument.RootElement.GetProperty("items");
        Assert.Contains(unratedItems.EnumerateArray(), item => item.GetProperty("targetId").GetGuid() == unratedTrackId);
        Assert.All(unratedItems.EnumerateArray(), item => Assert.True(item.GetProperty("value").ValueKind is JsonValueKind.Null));
        Assert.Equal(HttpStatusCode.BadRequest, invalidScopeResponse.StatusCode);
        Assert.Equal("rating_showcase.scope_invalid", invalidScopeDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Rating showcases support artist release and label targets")]
    public async Task Rating_showcases_support_artist_release_and_label_targets()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid criterionId = await CreateRatingCriterionAsync(client, "archive-score", "Archive score", ArtistReleaseLabelTargetTypes);
        Guid ratedArtistId = await CreateArtistAsync(client, "Autechre");
        Guid unratedArtistId = await CreateArtistAsync(client, "Boards of Canada");
        Guid ratedReleaseId = await CreateReleaseAsync(client, "Tri Repetae");
        Guid unratedReleaseId = await CreateReleaseAsync(client, "Music Has the Right to Children");
        Guid ratedLabelId = await CreateLabelAsync(client, "Warp");
        Guid unratedLabelId = await CreateLabelAsync(client, "Skam");

        await RateAsync(client, "artist", ratedArtistId, criterionId, 9);
        await RateAsync(client, "release", ratedReleaseId, criterionId, 8);
        await RateAsync(client, "label", ratedLabelId, criterionId, 7);

        await AssertShowcaseAsync(client, criterionId, "artist", "top", ratedArtistId, 9);
        await AssertShowcaseAsync(client, criterionId, "artist", "unrated", unratedArtistId, null);
        await AssertShowcaseAsync(client, criterionId, "release", "top", ratedReleaseId, 8);
        await AssertShowcaseAsync(client, criterionId, "release", "unrated", unratedReleaseId, null);
        await AssertShowcaseAsync(client, criterionId, "label", "top", ratedLabelId, 7);
        await AssertShowcaseAsync(client, criterionId, "label", "unrated", unratedLabelId, null);
    }

    private static async Task<Guid> CreateRatingCriterionAsync(HttpClient client, string code, string name, string[] targetTypes)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/rating-criteria",
            new { code, name, targetTypes, sortOrder = 40, isActive = true });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task RateAsync(HttpClient client, string targetType, Guid targetId, Guid criterionId, int value)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync(
            $"/api/ratings/{targetType}/{targetId}/{criterionId}",
            new { value });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task AssertShowcaseAsync(
        HttpClient client,
        Guid criterionId,
        string targetType,
        string mode,
        Guid expectedTargetId,
        int? expectedValue)
    {
        using HttpResponseMessage response = await client.GetAsync(
            $"/api/rating-showcases?criterionId={criterionId}&targetType={targetType}&mode={mode}&limit=10&offset=0");
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement item = document.RootElement.GetProperty("items")[0];
        Assert.Equal(expectedTargetId, item.GetProperty("targetId").GetGuid());
        Assert.Equal(targetType, item.GetProperty("targetType").GetString());
        if (expectedValue.HasValue)
        {
            Assert.Equal(expectedValue.Value, item.GetProperty("value").GetInt32());
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("value").ValueKind);
        }
    }
}
