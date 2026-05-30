using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class CatalogLinksEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public CatalogLinksEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Catalog links return compact selector matches across requested kinds")]
    public async Task Catalog_links_return_compact_selector_matches_across_requested_kinds()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid labelId = await CreateLabelAsync(client, "Factory Records");
        Guid playlistId = await CreatePlaylistAsync(client, "Factory shelf");
        _ = await CreateArtistAsync(client, "Unrelated Artist");

        using HttpResponseMessage response = await client.GetAsync("/api/catalog-links?query=Factory&kinds=label,playlist&limit=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] items = [.. document.RootElement.GetProperty("items").EnumerateArray()];
        Assert.Contains(items, item => IsLink(item, "label", labelId, "Factory Records"));
        Assert.Contains(items, item => IsLink(item, "playlist", playlistId, "Factory shelf"));
        Assert.DoesNotContain(items, item => item.GetProperty("kind").GetString() == "artist");
    }

    [Fact(DisplayName = "Owned item catalog links filter by target title before limiting")]
    public async Task Owned_item_catalog_links_filter_by_target_title_before_limiting()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        for (int index = 0; index < 6; index++)
        {
            Guid releaseId = await CreateReleaseAsync(client, $"Shelf filler {index:D2}");
            _ = await CreateOwnedItemAsync(client, releaseId);
        }

        Guid matchingReleaseId = await CreateReleaseAsync(client, "Needle outside the first page");
        Guid matchingOwnedItemId = await CreateOwnedItemAsync(client, matchingReleaseId);

        using HttpResponseMessage response = await client.GetAsync("/api/catalog-links?query=Needle&kinds=ownedItem&limit=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement item = Assert.Single(document.RootElement.GetProperty("items").EnumerateArray());
        Assert.True(IsLink(item, "ownedItem", matchingOwnedItemId, "Needle outside the first page"), item.ToString());
    }

    private static bool IsLink(JsonElement item, string kind, Guid id, string title)
    {
        return item.GetProperty("kind").GetString() == kind &&
            item.GetProperty("id").GetGuid() == id &&
            item.GetProperty("title").GetString() == title;
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/labels", new { name });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePlaylistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/playlists",
            new { name, type = "manual", entries = Array.Empty<object>() });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = "vinyl", description = "LP" }
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
