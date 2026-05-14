using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class RatingEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ReleaseTrackTargetTypes = ["release", "track"];
    private static readonly string[] TrackTargetTypes = ["track"];
    private readonly PostgresFixture _postgres;

    public RatingEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Rating criteria endpoints expose defaults and manage custom criteria")]
    public async Task Rating_criteria_endpoints_expose_defaults_and_manage_custom_criteria()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage listResponse = await client.GetAsync("/api/rating-criteria");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);
        JsonElement overall = listDocument.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == "overall");

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/rating-criteria",
            new { code = "energy", name = "Energy", targetTypes = ReleaseTrackTargetTypes, sortOrder = 20, isActive = true });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid criterionId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/rating-criteria",
            new { code = "energy", name = "Energy again", targetTypes = TrackTargetTypes });
        using JsonDocument duplicateDocument = await ReadJsonAsync(duplicateResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/rating-criteria/{criterionId}",
            new { name = "Dancefloor Energy", targetTypes = TrackTargetTypes, sortOrder = 25, isActive = false });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage preserveInactiveResponse = await PatchAsJsonAsync(
            client,
            $"/api/rating-criteria/{criterionId}",
            new { name = "Floor Energy", sortOrder = 30 });
        using JsonDocument preserveInactiveDocument = await ReadJsonAsync(preserveInactiveResponse);

        Guid overallId = overall.GetProperty("id").GetGuid();
        using HttpResponseMessage protectedResponse = await client.PutAsJsonAsync(
            $"/api/rating-criteria/{overallId}",
            new { name = "Overall", targetTypes = ReleaseTrackTargetTypes, sortOrder = 10, isActive = false });
        using JsonDocument protectedDocument = await ReadJsonAsync(protectedResponse);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal("Overall", overall.GetProperty("name").GetString());
        Assert.True(overall.GetProperty("isBuiltin").GetBoolean());
        Assert.True(overall.GetProperty("isProtected").GetBoolean());
        Assert.False(overall.TryGetProperty("collectionId", out _));
        AssertTargetTypes(ReleaseTrackTargetTypes, overall.GetProperty("targetTypes"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("energy", createDocument.RootElement.GetProperty("code").GetString());
        AssertTargetTypes(ReleaseTrackTargetTypes, createDocument.RootElement.GetProperty("targetTypes"));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("rating_criterion.code_conflict", duplicateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Dancefloor Energy", updateDocument.RootElement.GetProperty("name").GetString());
        Assert.False(updateDocument.RootElement.GetProperty("isActive").GetBoolean());
        AssertTargetTypes(TrackTargetTypes, updateDocument.RootElement.GetProperty("targetTypes"));
        Assert.Equal(HttpStatusCode.OK, preserveInactiveResponse.StatusCode);
        Assert.Equal("Floor Energy", preserveInactiveDocument.RootElement.GetProperty("name").GetString());
        Assert.False(preserveInactiveDocument.RootElement.GetProperty("isActive").GetBoolean());
        AssertTargetTypes(TrackTargetTypes, preserveInactiveDocument.RootElement.GetProperty("targetTypes"));
        Assert.Equal(HttpStatusCode.BadRequest, protectedResponse.StatusCode);
        Assert.Equal("rating_criterion.protected", protectedDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Rating value endpoints upsert delete and validate target ratings")]
    public async Task Rating_value_endpoints_upsert_delete_and_validate_target_ratings()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid criterionId = await FindOverallCriterionIdAsync(client);
        Guid trackId = await CreateTrackAsync(client, "Age of Consent");

        using HttpResponseMessage upsertResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{trackId}/{criterionId}",
            new { value = 9 });
        using JsonDocument upsertDocument = await ReadJsonAsync(upsertResponse);

        using HttpResponseMessage listResponse = await client.GetAsync($"/api/ratings?targetType=track&targetId={trackId}");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpResponseMessage invalidValueResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{trackId}/{criterionId}",
            new { value = 11 });
        using JsonDocument invalidValueDocument = await ReadJsonAsync(invalidValueResponse);

        using HttpResponseMessage missingTargetResponse = await client.PutAsJsonAsync(
            $"/api/ratings/track/{Guid.NewGuid()}/{criterionId}",
            new { value = 8 });

        using HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/ratings/track/{trackId}/{criterionId}");
        using HttpResponseMessage emptyListResponse = await client.GetAsync($"/api/ratings?targetType=track&targetId={trackId}");
        using JsonDocument emptyListDocument = await ReadJsonAsync(emptyListResponse);

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);
        Assert.Equal("track", upsertDocument.RootElement.GetProperty("targetType").GetString());
        Assert.Equal(trackId, upsertDocument.RootElement.GetProperty("targetId").GetGuid());
        Assert.Equal(9, upsertDocument.RootElement.GetProperty("value").GetInt32());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(criterionId, listDocument.RootElement.GetProperty("items")[0].GetProperty("criterionId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, invalidValueResponse.StatusCode);
        Assert.Equal("rating.out_of_range", invalidValueDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missingTargetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(0, emptyListDocument.RootElement.GetProperty("total").GetInt32());
    }

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

    [Fact(DisplayName = "Rating endpoints preserve collection isolation")]
    public async Task Rating_endpoints_preserve_collection_isolation()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminCriterionId = await FindOverallCriterionIdAsync(adminClient);
        Guid userCriterionId = await FindOverallCriterionIdAsync(userClient);
        Guid adminTrackId = await CreateTrackAsync(adminClient, "Same Name");
        _ = await CreateTrackAsync(userClient, "Same Name");

        using HttpResponseMessage foreignTargetResponse = await userClient.PutAsJsonAsync(
            $"/api/ratings/track/{adminTrackId}/{userCriterionId}",
            new { value = 7 });
        using HttpResponseMessage foreignCriterionResponse = await userClient.PutAsJsonAsync(
            $"/api/ratings/track/{adminTrackId}/{adminCriterionId}",
            new { value = 7 });
        using HttpResponseMessage userRatingsResponse = await userClient.GetAsync("/api/ratings?targetType=track");
        using JsonDocument userRatingsDocument = await ReadJsonAsync(userRatingsResponse);

        Assert.Equal(HttpStatusCode.NotFound, foreignTargetResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignCriterionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, userRatingsResponse.StatusCode);
        Assert.Equal(0, userRatingsDocument.RootElement.GetProperty("total").GetInt32());
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

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, durationSeconds = 300, genres = Array.Empty<string>(), tags = Array.Empty<string>() });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync(
            "/api/auth/register",
            new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync(
            "/api/admin/users",
            new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync(
            "/api/auth/login",
            new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static void AssertTargetTypes(string[] expected, JsonElement targetTypes)
    {
        string[] actual = [.. targetTypes.EnumerateArray().Select(targetType => targetType.GetString() ?? string.Empty)];
        Assert.Equal(expected, actual);
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

    private static async Task<HttpResponseMessage> PatchAsJsonAsync<TValue>(HttpClient client, string requestUri, TValue value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(value)
        };

        return await client.SendAsync(request);
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
}
