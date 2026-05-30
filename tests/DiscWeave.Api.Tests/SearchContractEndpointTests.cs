using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class SearchContractEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public SearchContractEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Search accepts q as a query alias")]
    public async Task Search_accepts_q_as_a_query_alias()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Alias Query Release");

        using HttpResponseMessage response = await client.GetAsync("/api/search?q=Alias%20Query&limit=20&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release" && result.GetProperty("id").GetGuid() == releaseId);
        Assert.False(item.TryGetProperty("collectionId", out _));
    }

    [Theory(DisplayName = "Search rejects invalid constrained parameters with deterministic error codes")]
    [InlineData("entityType=unknown", "search.entity_type_invalid")]
    [InlineData("status=archived", "search.status_invalid")]
    [InlineData("savedView=surprises", "search.saved_view_invalid")]
    public async Task Search_rejects_invalid_constrained_parameters_with_deterministic_error_codes(
        string queryString,
        string expectedCode)
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync($"/api/search?{queryString}&limit=20&offset=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Search composes valid saved views and status filters")]
    public async Task Search_composes_valid_saved_views_and_status_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid ownedReleaseId = await CreateReleaseAsync(client, "Owned Filter Release");
        Guid wantedReleaseId = await CreateReleaseAsync(client, "Wanted Filter Release");
        _ = await CreateOwnedItemAsync(client, ownedReleaseId, "owned");
        _ = await CreateOwnedItemAsync(client, wantedReleaseId, "wanted");

        using HttpResponseMessage response = await client.GetAsync("/api/search?savedView=all&entityType=release&status=owned&limit=20&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("type").GetString() == "release");
        Assert.Equal(ownedReleaseId, item.GetProperty("id").GetGuid());
        Assert.DoesNotContain(
            document.RootElement.GetProperty("items").EnumerateArray(),
            result => result.GetProperty("id").GetGuid() == wantedReleaseId);
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string status)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status,
                medium = new { type = "vinyl", description = "12 inch" }
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
