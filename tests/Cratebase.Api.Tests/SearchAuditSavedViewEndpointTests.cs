using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class SearchAuditSavedViewEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public SearchAuditSavedViewEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Search audit saved views match collector gaps across owned copies")]
    public async Task Search_audit_saved_views_match_collector_gaps_across_owned_copies()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid physicalOnlyReleaseId = await CreateReleaseAsync(client, "Physical Only Release");
        Guid physicalOnlyItemId = await CreateOwnedItemAsync(client, physicalOnlyReleaseId, "owned", "vinyl");

        Guid physicalWithDigitalReleaseId = await CreateReleaseAsync(client, "Physical With Digital Release");
        Guid physicalWithDigitalVinylItemId = await CreateOwnedItemAsync(client, physicalWithDigitalReleaseId, "owned", "vinyl");
        _ = await CreateDigitalOwnedItemAsync(client, physicalWithDigitalReleaseId, "owned", "flac");

        Guid lossyOnlyReleaseId = await CreateReleaseAsync(client, "Lossy Only Release");
        Guid lossyOnlyItemId = await CreateDigitalOwnedItemAsync(client, lossyOnlyReleaseId, "owned", "mp3");

        Guid lossyWithLosslessReleaseId = await CreateReleaseAsync(client, "Lossy With Lossless Release");
        Guid lossyWithLosslessItemId = await CreateDigitalOwnedItemAsync(client, lossyWithLosslessReleaseId, "owned", "mp3");
        _ = await CreateDigitalOwnedItemAsync(client, lossyWithLosslessReleaseId, "owned", "flac");

        Guid wantedOnlyReleaseId = await CreateReleaseAsync(client, "Wanted Only Release");
        Guid wantedOnlyItemId = await CreateOwnedItemAsync(client, wantedOnlyReleaseId, "wanted", "vinyl");

        Guid wantedWithOwnedReleaseId = await CreateReleaseAsync(client, "Wanted With Owned Release");
        Guid wantedWithOwnedItemId = await CreateOwnedItemAsync(client, wantedWithOwnedReleaseId, "wanted", "vinyl");
        _ = await CreateOwnedItemAsync(client, wantedWithOwnedReleaseId, "owned", "vinyl");

        using JsonDocument physicalWithoutDigital = await SearchAsync(client, "physicalWithoutDigital");
        Assert.Contains(SearchItems(physicalWithoutDigital), result => IsResult(result, "release", physicalOnlyReleaseId));
        Assert.Contains(SearchItems(physicalWithoutDigital), result => IsResult(result, "ownedItem", physicalOnlyItemId));
        Assert.DoesNotContain(SearchItems(physicalWithoutDigital), result => IsResult(result, "release", physicalWithDigitalReleaseId));
        Assert.DoesNotContain(SearchItems(physicalWithoutDigital), result => IsResult(result, "ownedItem", physicalWithDigitalVinylItemId));

        using JsonDocument lossyWithoutLossless = await SearchAsync(client, "lossyWithoutLossless");
        Assert.Contains(SearchItems(lossyWithoutLossless), result => IsResult(result, "release", lossyOnlyReleaseId));
        Assert.Contains(SearchItems(lossyWithoutLossless), result => IsResult(result, "ownedItem", lossyOnlyItemId));
        Assert.DoesNotContain(SearchItems(lossyWithoutLossless), result => IsResult(result, "release", lossyWithLosslessReleaseId));
        Assert.DoesNotContain(SearchItems(lossyWithoutLossless), result => IsResult(result, "ownedItem", lossyWithLosslessItemId));

        using JsonDocument wantedNotOwned = await SearchAsync(client, "wantedNotOwned");
        Assert.Contains(SearchItems(wantedNotOwned), result => IsResult(result, "release", wantedOnlyReleaseId));
        Assert.Contains(SearchItems(wantedNotOwned), result => IsResult(result, "ownedItem", wantedOnlyItemId));
        Assert.DoesNotContain(SearchItems(wantedNotOwned), result => IsResult(result, "release", wantedWithOwnedReleaseId));
        Assert.DoesNotContain(SearchItems(wantedNotOwned), result => IsResult(result, "ownedItem", wantedWithOwnedItemId));
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
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

    private static async Task<Guid> CreateDigitalOwnedItemAsync(HttpClient client, Guid releaseId, string status, string format)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status,
                medium = new
                {
                    type = "digital",
                    path = $"/music/{releaseId:N}-{format}.audio",
                    format
                }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> SearchAsync(HttpClient client, string savedView)
    {
        using HttpResponseMessage response = await client.GetAsync($"/api/search?savedView={savedView}&limit=50&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private static JsonElement.ArrayEnumerator SearchItems(JsonDocument document)
    {
        return document.RootElement.GetProperty("items").EnumerateArray();
    }

    private static bool IsResult(JsonElement result, string type, Guid id)
    {
        return result.GetProperty("type").GetString() == type && result.GetProperty("id").GetGuid() == id;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
