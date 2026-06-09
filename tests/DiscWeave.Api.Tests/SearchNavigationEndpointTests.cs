using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class SearchNavigationEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public SearchNavigationEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Search supports filters and saved views without a query")]
    public async Task Search_supports_filters_and_saved_views_without_a_query()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid producerId = await CreateArtistAsync(client, "Martin Hannett");
        Guid releaseId = await CreateReleaseAsync(client, "Unknown Pleasures", tags: ["post-punk"]);
        _ = await CreateCreditAsync(client, producerId, "release", releaseId, "producer");
        _ = await CreateOwnedItemAsync(client, releaseId, "needsDigitization", "vinyl");

        using HttpResponseMessage roleResponse = await client.GetAsync("/api/search?entityType=release&role=producer&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, roleResponse.StatusCode);
        using JsonDocument roleDocument = await ReadJsonAsync(roleResponse);
        JsonElement roleItem = Assert.Single(roleDocument.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("release", roleItem.GetProperty("type").GetString());
        Assert.Equal(releaseId, roleItem.GetProperty("id").GetGuid());
        Assert.Equal("producer", roleItem.GetProperty("facets").GetProperty("roles")[0].GetString());

        using HttpResponseMessage creditsViewResponse = await client.GetAsync("/api/search?savedView=credits&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, creditsViewResponse.StatusCode);
        using JsonDocument creditsViewDocument = await ReadJsonAsync(creditsViewResponse);
        Assert.Contains(
            creditsViewDocument.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);

        using HttpResponseMessage savedViewResponse = await client.GetAsync("/api/search?savedView=needsDigitization&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, savedViewResponse.StatusCode);
        using JsonDocument savedViewDocument = await ReadJsonAsync(savedViewResponse);
        JsonElement savedViewItem = Assert.Single(
            savedViewDocument.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "ownedItem");
        Assert.Equal("ownedItem", savedViewItem.GetProperty("type").GetString());
        Assert.Equal("needsDigitization", savedViewItem.GetProperty("facets").GetProperty("statuses")[0].GetString());

        using HttpResponseMessage allViewResponse = await client.GetAsync("/api/search?savedView=all&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, allViewResponse.StatusCode);
        using JsonDocument allViewDocument = await ReadJsonAsync(allViewResponse);
        Assert.Contains(
            allViewDocument.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
    }

    [Fact(DisplayName = "Search supports trigram matches and richer result context")]
    public async Task Search_supports_trigram_matches_and_richer_result_context()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid releaseId = await CreateReleaseAsync(client, "Confusion", tags: ["factory"]);

        using HttpResponseMessage response = await client.GetAsync("/api/search?query=Confuson&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
        Assert.True(item.GetProperty("rank").GetDecimal() > 0);
        Assert.Contains("title", item.GetProperty("matchedFields").EnumerateArray().Select(field => field.GetString()));
        Assert.Contains(
            item.GetProperty("snippets").EnumerateArray(),
            snippet => snippet.GetString()?.Contains("Confusion", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact(DisplayName = "Search treats wildcard characters as literal query text")]
    public async Task Search_treats_wildcard_characters_as_literal_query_text()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        _ = await CreateReleaseAsync(client, "Literal Wildcard Control");

        using HttpResponseMessage response = await client.GetAsync("/api/search?query=%25&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(0, document.RootElement.GetProperty("total").GetInt32());
        Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact(DisplayName = "Search label filter matches every release label")]
    public async Task Search_label_filter_matches_every_release_label()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid primaryLabelId = await CreateLabelAsync(client, "Primary Filter Label");
        Guid secondaryLabelId = await CreateLabelAsync(client, "Secondary Filter Label");
        Guid releaseId = await CreateReleaseWithLabelsAsync(client, "Multi Label Release", [primaryLabelId, secondaryLabelId]);

        using HttpResponseMessage response = await client.GetAsync($"/api/search?entityType=release&labelId={secondaryLabelId}&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
        Assert.Equal(primaryLabelId, item.GetProperty("facets").GetProperty("labelId").GetGuid());
    }

    [Fact(DisplayName = "Search documents update immediately after catalog writes")]
    public async Task Search_documents_update_immediately_after_catalog_writes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Before Rename Records");
        Guid releaseId = await CreateReleaseAsync(client, "Fresh Index Release", labelId);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/labels/{labelId}", new { name = "After Rename Records" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using HttpResponseMessage response = await client.GetAsync("/api/search?query=After%20Rename&entityType=release&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
        Assert.Contains("label", item.GetProperty("matchedFields").EnumerateArray().Select(field => field.GetString()));
    }

    [Fact(DisplayName = "Search documents update immediately after release label changes")]
    public async Task Search_documents_update_immediately_after_release_label_changes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid oldLabelId = await CreateLabelAsync(client, "Old Label Navigation");
        Guid newLabelId = await CreateLabelAsync(client, "New Label Navigation");
        Guid releaseId = await CreateReleaseWithLabelsAsync(client, "Relabeled Search Release", [oldLabelId]);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            ReleaseWithLabelsRequest("Relabeled Search Release", [newLabelId]));
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using HttpResponseMessage newLabelResponse = await client.GetAsync($"/api/search?entityType=release&labelId={newLabelId}&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, newLabelResponse.StatusCode);
        using JsonDocument newLabelDocument = await ReadJsonAsync(newLabelResponse);
        Assert.Contains(
            newLabelDocument.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);

        using HttpResponseMessage oldLabelResponse = await client.GetAsync($"/api/search?entityType=release&labelId={oldLabelId}&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, oldLabelResponse.StatusCode);
        using JsonDocument oldLabelDocument = await ReadJsonAsync(oldLabelResponse);
        Assert.DoesNotContain(
            oldLabelDocument.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
    }

    [Fact(DisplayName = "Search rejects invalid query parameter shapes")]
    public async Task Search_rejects_invalid_query_parameter_shapes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage labelResponse = await client.GetAsync("/api/search?query=anything&labelId=not-a-guid");
        using HttpResponseMessage limitResponse = await client.GetAsync("/api/search?query=anything&limit=wide");
        using HttpResponseMessage offsetResponse = await client.GetAsync("/api/search?query=anything&offset=late");

        Assert.Equal(HttpStatusCode.BadRequest, labelResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, limitResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, offsetResponse.StatusCode);
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
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

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid? labelId = null, IReadOnlyList<string>? tags = null)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, labelId, year = 1983, genres = EmptyStrings, tags = tags ?? EmptyStrings });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseWithLabelsAsync(HttpClient client, string title, IReadOnlyList<Guid> labelIds)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/releases", ReleaseWithLabelsRequest(title, labelIds));
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static object ReleaseWithLabelsRequest(string title, IReadOnlyList<Guid> labelIds)
    {
        return new
        {
            title,
            type = "standalone",
            isVariousArtists = true,
            notOnLabel = false,
            labels = labelIds.Select(labelId => new { labelId, catalogNumber = (string?)null, hasNoCatalogNumber = false }).ToArray(),
            year = 1983,
            genres = EmptyStrings,
            tags = EmptyStrings
        };
    }

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, string targetType, Guid targetId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId, targetType, targetId, roles = new[] { role } });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status, string medium)
    {
        return await CreateOwnedItemAsync(client, "release", releaseId, status, medium);
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, string targetType, Guid targetId, string status, string medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType,
                targetId,
                status,
                medium = new { type = medium, description = medium }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

}
