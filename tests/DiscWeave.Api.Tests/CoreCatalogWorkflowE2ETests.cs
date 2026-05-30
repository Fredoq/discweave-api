using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class CoreCatalogWorkflowE2ETests : IClassFixture<PostgresFixture>
{
    private static readonly string[] ClassicTags = ["classic"];
    private static readonly string[] FactoryTags = ["factory"];
    private static readonly string[] OpenerTags = ["opener"];
    private static readonly string[] PostPunkGenres = ["Post-punk"];
    private static readonly string[] RemasterTags = ["remaster"];
    private readonly PostgresFixture _postgres;

    public CoreCatalogWorkflowE2ETests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Label endpoints support the full cataloging workflow")]
    public async Task Label_endpoints_support_the_full_cataloging_workflow()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/labels", new { name = "  Factory  " });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid labelId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/labels/{labelId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/labels/{labelId}", new { name = "Factory Records" });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/labels?search=factory&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/labels/{labelId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"label:{labelId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal("Factory", createDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(labelId, getDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Factory Records", updateDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Track endpoints support the full cataloging workflow")]
    public async Task Track_endpoints_support_the_full_cataloging_workflow()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title = "  Age of Consent  ", durationSeconds = 316, genres = PostPunkGenres, tags = OpenerTags });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid trackId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/tracks/{trackId}",
            new { title = "Age of Consent (2020 Remaster)", durationSeconds = 317, genres = PostPunkGenres, tags = RemasterTags });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage getUpdatedResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument getUpdatedDocument = await ReadJsonAsync(getUpdatedResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/tracks?search=consent&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal("Age of Consent", createDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(316, createDocument.RootElement.GetProperty("durationSeconds").GetInt32());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(trackId, updateDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Age of Consent (2020 Remaster)", updateDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal(317, updateDocument.RootElement.GetProperty("durationSeconds").GetInt32());
        AssertStringArray(RemasterTags, updateDocument.RootElement.GetProperty("tags"));
        Assert.Equal(HttpStatusCode.OK, getUpdatedResponse.StatusCode);
        AssertStringArray(RemasterTags, getUpdatedDocument.RootElement.GetProperty("tags"));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Track endpoints return tracks without optional duration")]
    public async Task Track_endpoints_return_tracks_without_optional_duration()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title = "Untimed Field Recording", genres = Array.Empty<string>(), tags = Array.Empty<string>() });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid trackId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage getResponse = await client.GetAsync($"/api/tracks/{trackId}");
        using JsonDocument getDocument = await ReadJsonAsync(getResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/tracks?search=untimed&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.True(getDocument.RootElement.GetProperty("durationSeconds").ValueKind is JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.True(listDocument.RootElement.GetProperty("items")[0].GetProperty("durationSeconds").ValueKind is JsonValueKind.Null);
    }

    [Fact(DisplayName = "Release endpoints support the full cataloging workflow")]
    public async Task Release_endpoints_support_the_full_cataloging_workflow()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(client);

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new { title = "  Power, Corruption & Lies  ", type = "album", isVariousArtists = true, labelId, year = 1983, genres = PostPunkGenres, tags = FactoryTags });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid releaseId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            new { title = "Power Corruption and Lies", type = "album", isVariousArtists = true, labelId, year = 1983, genres = PostPunkGenres, tags = ClassicTags });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage getUpdatedResponse = await client.GetAsync($"/api/releases/{releaseId}");
        using JsonDocument getUpdatedDocument = await ReadJsonAsync(getUpdatedResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/releases?search=power&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        Assert.Equal("Power, Corruption & Lies", createDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal("album", createDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(labelId, createDocument.RootElement.GetProperty("labelId").GetGuid());
        Assert.Equal(1983, createDocument.RootElement.GetProperty("year").GetInt32());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Power Corruption and Lies", updateDocument.RootElement.GetProperty("title").GetString());
        AssertStringArray(ClassicTags, updateDocument.RootElement.GetProperty("tags"));
        Assert.Equal(HttpStatusCode.OK, getUpdatedResponse.StatusCode);
        AssertStringArray(ClassicTags, getUpdatedDocument.RootElement.GetProperty("tags"));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
    }

    [Fact(DisplayName = "Owned item endpoints support the full collection workflow")]
    public async Task Owned_item_endpoints_support_the_full_collection_workflow()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client);

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = "vinyl", description = "LP" },
                condition = "veryGoodPlus",
                storageLocation = "Shelf A"
            });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Guid ownedItemId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/owned-items/{ownedItemId}",
            new { status = "needsDigitization", condition = "veryGood", storageLocation = "Digitization queue" });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage listResponse = await client.GetAsync("/api/owned-items?status=needsDigitization&medium=vinyl&limit=10&offset=0");
        using JsonDocument listDocument = await ReadJsonAsync(listResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/owned-items/{ownedItemId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"owned-item:{ownedItemId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal("owned", createDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("vinyl", createDocument.RootElement.GetProperty("medium").GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("needsDigitization", updateDocument.RootElement.GetProperty("status").GetString());
        Assert.Equal("Digitization queue", updateDocument.RootElement.GetProperty("storageLocation").GetString());
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Equal(1, listDocument.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Creating an owned item without a medium returns a validation error")]
    public async Task Creating_an_owned_item_without_a_medium_returns_a_validation_error()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = (object?)null
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("owned_item.request_invalid", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("Owned item request is invalid", document.RootElement.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "Creating an owned item for a missing target returns a conflict")]
    public async Task Creating_an_owned_item_for_a_missing_target_returns_a_conflict()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = Guid.CreateVersion7(),
                status = "owned",
                medium = new { type = "vinyl", description = "LP" }
            });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("owned_item.target_conflict", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name = "Factory" });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/releases", new { title = "Power, Corruption & Lies", type = "album", isVariousArtists = true, year = 1983 });
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

    private static void AssertStringArray(IReadOnlyList<string> expected, JsonElement actual)
    {
        Assert.Equal(expected, [.. actual.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]);
    }
}
