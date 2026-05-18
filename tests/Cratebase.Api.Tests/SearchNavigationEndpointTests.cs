using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

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

    [Fact(DisplayName = "Catalog graph context describes label releases and owned coverage")]
    public async Task Catalog_graph_context_describes_label_releases_and_owned_coverage()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid releaseId = await CreateReleaseAsync(client, "Blue Monday", labelId);
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "owned", "vinyl");

        using HttpResponseMessage response = await client.GetAsync($"/api/catalog-graph/label/{labelId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal("label", document.RootElement.GetProperty("entity").GetProperty("type").GetString());
        Assert.Equal(labelId, document.RootElement.GetProperty("entity").GetProperty("id").GetGuid());
        JsonElement releases = document.RootElement.GetProperty("sections").GetProperty("releases");
        JsonElement releaseLink = Assert.Single(releases.EnumerateArray());
        Assert.Equal(releaseId, releaseLink.GetProperty("id").GetGuid());
        JsonElement ownedCopies = document.RootElement.GetProperty("sections").GetProperty("ownedCopies");
        JsonElement ownedCopyLink = Assert.Single(ownedCopies.EnumerateArray());
        Assert.Equal(ownedItemId, ownedCopyLink.GetProperty("id").GetGuid());
        Assert.Contains(
            document.RootElement.GetProperty("collectorSignals").EnumerateArray(),
            signal => signal.GetString() == "vinyl");
    }

    [Fact(DisplayName = "Catalog graph context only returns entities from the current collection")]
    public async Task Catalog_graph_context_only_returns_entities_from_the_current_collection()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);

        Guid adminLabelId = await CreateLabelAsync(adminClient, "Private Label");

        using HttpResponseMessage response = await userClient.GetAsync($"/api/catalog-graph/label/{adminLabelId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync("/api/auth/register", new AuthRequest("owner@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("collector@example.com", "Password1!", false));
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);

        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync("/api/auth/login", new AuthRequest("collector@example.com", "Password1!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
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

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, string targetType, Guid targetId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/credits", new { contributorArtistId, targetType, targetId, role });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status, string medium)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
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

    private sealed record AuthRequest(string Email, string Password);

    private sealed record CreateUserRequest(string Email, string Password, bool IsAdmin);
}
