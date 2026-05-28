using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class OwnedItemInventoryEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public OwnedItemInventoryEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Owned item inventory filters by status medium condition and storage location")]
    public async Task Owned_item_inventory_filters_by_status_medium_condition_and_storage_location()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid matchingReleaseId = await CreateReleaseAsync(client, "Matching Copy");
        Guid matchingItemId = await CreateOwnedItemAsync(client, "release", matchingReleaseId, "owned", "vinyl", "veryGood", "Shelf A3");
        Guid wrongConditionReleaseId = await CreateReleaseAsync(client, "Wrong Condition Copy");
        Guid wrongConditionItemId = await CreateOwnedItemAsync(client, "release", wrongConditionReleaseId, "owned", "vinyl", "good", "Shelf A3");
        Guid wrongStorageReleaseId = await CreateReleaseAsync(client, "Wrong Storage Copy");
        Guid wrongStorageItemId = await CreateOwnedItemAsync(client, "release", wrongStorageReleaseId, "owned", "vinyl", "veryGood", "Overflow Box");
        Guid wrongStatusReleaseId = await CreateReleaseAsync(client, "Wrong Status Copy");
        Guid wrongStatusItemId = await CreateOwnedItemAsync(client, "release", wrongStatusReleaseId, "wanted", "vinyl", "veryGood", "Shelf A3");

        using JsonDocument document = await GetJsonAsync(
            client,
            "/api/owned-items?status=owned&medium=vinyl&condition=veryGood&storageLocation=shelf%20a&limit=20&offset=0",
            HttpStatusCode.OK);

        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement item = FindItem(document, matchingItemId);
        Assert.Equal("veryGood", item.GetProperty("condition").GetString());
        Assert.Equal("Shelf A3", item.GetProperty("storageLocation").GetString());
        AssertNoItem(document, wrongConditionItemId);
        AssertNoItem(document, wrongStorageItemId);
        AssertNoItem(document, wrongStatusItemId);
    }

    [Fact(DisplayName = "Owned item inventory views return target-level collector gaps")]
    public async Task Owned_item_inventory_views_return_target_level_collector_gaps()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        await CreateMediaDictionaryEntryAsync(client, "bandcamp", "Bandcamp", "digital");
        Guid physicalOnlyReleaseId = await CreateReleaseAsync(client, "Physical Only");
        Guid physicalOnlyItemId = await CreateOwnedItemAsync(client, "release", physicalOnlyReleaseId, "owned", "vinyl");
        Guid physicalWithDigitalReleaseId = await CreateReleaseAsync(client, "Physical With Digital");
        Guid physicalWithDigitalItemId = await CreateOwnedItemAsync(client, "release", physicalWithDigitalReleaseId, "owned", "vinyl");
        _ = await CreateDigitalOwnedItemAsync(client, "release", physicalWithDigitalReleaseId, "flac");
        Guid physicalWithCustomDigitalReleaseId = await CreateReleaseAsync(client, "Physical With Custom Digital");
        Guid physicalWithCustomDigitalItemId = await CreateOwnedItemAsync(client, "release", physicalWithCustomDigitalReleaseId, "owned", "vinyl");
        _ = await CreateDigitalOwnedItemAsync(client, "release", physicalWithCustomDigitalReleaseId, "flac", "bandcamp");
        Guid lossyOnlyReleaseId = await CreateReleaseAsync(client, "Lossy Only");
        Guid lossyOnlyItemId = await CreateDigitalOwnedItemAsync(client, "release", lossyOnlyReleaseId, "mp3");
        Guid customLossyOnlyReleaseId = await CreateReleaseAsync(client, "Custom Lossy Only");
        Guid customLossyOnlyItemId = await CreateDigitalOwnedItemAsync(client, "release", customLossyOnlyReleaseId, "mp3", "bandcamp");
        Guid lossyWithLosslessReleaseId = await CreateReleaseAsync(client, "Lossy With Lossless");
        Guid lossyWithLosslessItemId = await CreateDigitalOwnedItemAsync(client, "release", lossyWithLosslessReleaseId, "mp3");
        _ = await CreateDigitalOwnedItemAsync(client, "release", lossyWithLosslessReleaseId, "flac");
        Guid wantedOnlyReleaseId = await CreateReleaseAsync(client, "Wanted Only");
        Guid wantedOnlyItemId = await CreateOwnedItemAsync(client, "release", wantedOnlyReleaseId, "wanted", "vinyl");
        Guid wantedWithOwnedReleaseId = await CreateReleaseAsync(client, "Wanted With Owned");
        Guid wantedWithOwnedItemId = await CreateOwnedItemAsync(client, "release", wantedWithOwnedReleaseId, "wanted", "vinyl");
        _ = await CreateOwnedItemAsync(client, "release", wantedWithOwnedReleaseId, "owned", "vinyl");
        Guid needsDigitizationReleaseId = await CreateReleaseAsync(client, "Digitization Queue");
        Guid needsDigitizationItemId = await CreateOwnedItemAsync(client, "release", needsDigitizationReleaseId, "needsDigitization", "cassette");

        using JsonDocument physicalWithoutDigital = await GetJsonAsync(client, "/api/owned-items?inventoryView=physicalWithoutDigital&limit=20&offset=0", HttpStatusCode.OK);
        AssertSignal(FindItem(physicalWithoutDigital, physicalOnlyItemId), "physicalWithoutDigital");
        AssertNoItem(physicalWithoutDigital, physicalWithDigitalItemId);
        AssertNoItem(physicalWithoutDigital, physicalWithCustomDigitalItemId);

        using JsonDocument lossyWithoutLossless = await GetJsonAsync(client, "/api/owned-items?inventoryView=lossyWithoutLossless&limit=20&offset=0", HttpStatusCode.OK);
        AssertSignal(FindItem(lossyWithoutLossless, lossyOnlyItemId), "lossyWithoutLossless");
        JsonElement customLossyOnlyItem = FindItem(lossyWithoutLossless, customLossyOnlyItemId);
        AssertSignal(customLossyOnlyItem, "lossyWithoutLossless");
        AssertSignalsAreSorted(customLossyOnlyItem);
        AssertNoItem(lossyWithoutLossless, lossyWithLosslessItemId);

        using JsonDocument wantedNotOwned = await GetJsonAsync(client, "/api/owned-items?inventoryView=wantedNotOwned&limit=20&offset=0", HttpStatusCode.OK);
        AssertSignal(FindItem(wantedNotOwned, wantedOnlyItemId), "wantedNotOwned");
        AssertNoItem(wantedNotOwned, wantedWithOwnedItemId);

        using JsonDocument needsDigitization = await GetJsonAsync(client, "/api/owned-items?inventoryView=needsDigitization&limit=20&offset=0", HttpStatusCode.OK);
        AssertSignal(FindItem(needsDigitization, needsDigitizationItemId), "needsDigitization");
    }

    [Fact(DisplayName = "Owned item inventory responses include release and track target summaries")]
    public async Task Owned_item_inventory_responses_include_release_and_track_target_summaries()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Release Target");
        Guid releaseItemId = await CreateOwnedItemAsync(client, "release", releaseId, "owned", "vinyl");
        Guid trackId = await CreateTrackAsync(client, "Track Target");
        Guid parentReleaseId = await CreateReleaseAsync(client, "Parent Release", trackId);
        Guid trackItemId = await CreateOwnedItemAsync(client, "track", trackId, "owned", "digital", format: "flac");

        using JsonDocument document = await GetJsonAsync(client, "/api/owned-items?limit=20&offset=0", HttpStatusCode.OK);

        JsonElement releaseTarget = FindItem(document, releaseItemId).GetProperty("target");
        Assert.Equal("release", releaseTarget.GetProperty("type").GetString());
        Assert.Equal(releaseId, releaseTarget.GetProperty("id").GetGuid());
        Assert.Equal("Release Target", releaseTarget.GetProperty("title").GetString());
        Assert.Equal(releaseId, releaseTarget.GetProperty("releaseId").GetGuid());
        Assert.Equal("Release Target", releaseTarget.GetProperty("releaseTitle").GetString());

        JsonElement trackTarget = FindItem(document, trackItemId).GetProperty("target");
        Assert.Equal("track", trackTarget.GetProperty("type").GetString());
        Assert.Equal(trackId, trackTarget.GetProperty("id").GetGuid());
        Assert.Equal("Track Target", trackTarget.GetProperty("title").GetString());
        Assert.Equal(parentReleaseId, trackTarget.GetProperty("releaseId").GetGuid());
        Assert.Equal("Parent Release", trackTarget.GetProperty("releaseTitle").GetString());
    }

    [Fact(DisplayName = "Owned item inventory rejects invalid condition and inventory view filters")]
    public async Task Owned_item_inventory_rejects_invalid_condition_and_inventory_view_filters()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument invalidCondition = await GetJsonAsync(client, "/api/owned-items?condition=sealed&limit=20&offset=0", HttpStatusCode.BadRequest);
        using JsonDocument invalidInventoryView = await GetJsonAsync(client, "/api/owned-items?inventoryView=streamingOnly&limit=20&offset=0", HttpStatusCode.BadRequest);

        Assert.Equal("owned_item.condition_invalid", invalidCondition.RootElement.GetProperty("code").GetString());
        Assert.Equal("owned_item.inventory_view_invalid", invalidInventoryView.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Owned item inventory filters and target summaries stay collection scoped")]
    public async Task Owned_item_inventory_filters_and_target_summaries_stay_collection_scoped()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        (HttpClient adminClient, HttpClient userClient) = await CreateAuthenticatedClientsAsync(host);
        Guid adminReleaseId = await CreateReleaseAsync(adminClient, "Foreign Inventory Target");
        _ = await CreateOwnedItemAsync(adminClient, "release", adminReleaseId, "owned", "vinyl", "veryGood", "Shared Shelf");
        Guid userReleaseId = await CreateReleaseAsync(userClient, "User Inventory Target");
        Guid userItemId = await CreateOwnedItemAsync(userClient, "release", userReleaseId, "owned", "vinyl", "veryGood", "Shared Shelf");

        using JsonDocument document = await GetJsonAsync(
            userClient,
            "/api/owned-items?condition=veryGood&storageLocation=shared&inventoryView=physicalWithoutDigital&limit=20&offset=0",
            HttpStatusCode.OK);

        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement target = FindItem(document, userItemId).GetProperty("target");
        Assert.Equal("User Inventory Target", target.GetProperty("title").GetString());
        Assert.DoesNotContain(
            document.RootElement.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("target").GetProperty("title").GetString() == "Foreign Inventory Target");
    }

    private static async Task<(HttpClient AdminClient, HttpClient UserClient)> CreateAuthenticatedClientsAsync(ApiTestHost host)
    {
        HttpClient adminClient = host.CreateClient();
        using HttpResponseMessage registerResponse = await adminClient.PostAsJsonAsync("/api/auth/register", new { email = "owner@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        using HttpResponseMessage createUserResponse = await adminClient.PostAsJsonAsync("/api/admin/users", new { email = "collector@example.com", password = "Password1!", isAdmin = false });
        Assert.Equal(HttpStatusCode.Created, createUserResponse.StatusCode);
        HttpClient userClient = host.CreateClient();
        using HttpResponseMessage loginResponse = await userClient.PostAsJsonAsync("/api/auth/login", new { email = "collector@example.com", password = "Password1!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        return (adminClient, userClient);
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid? trackId = null)
    {
        object request = trackId is { } value
            ? new { title, isVariousArtists = true, tracklist = new[] { new { trackId = value, position = 1 } } }
            : new { title, isVariousArtists = true };
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/releases", request), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/tracks", new { title }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateDigitalOwnedItemAsync(HttpClient client, string targetType, Guid targetId, string format, string medium = "digital")
    {
        return await CreateOwnedItemAsync(client, targetType, targetId, "owned", medium, format: format);
    }

    private static async Task<Guid> CreateOwnedItemAsync(
        HttpClient client,
        string targetType,
        Guid targetId,
        string status,
        string medium,
        string? condition = null,
        string? storageLocation = null,
        string? format = null)
    {
        object mediumRequest = format is not null
            ? new { type = medium, path = $"/music/{targetId:N}-{format}.audio", format = format ?? "flac" }
            : new { type = medium, description = medium };
        using JsonDocument document = await SendJsonAsync(
            client.PostAsJsonAsync("/api/owned-items", new { targetType, targetId, status, medium = mediumRequest, condition, storageLocation }),
            HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task CreateMediaDictionaryEntryAsync(HttpClient client, string code, string name, string mediaProfile)
    {
        using JsonDocument _ = await SendJsonAsync(
            client.PostAsJsonAsync("/api/settings/dictionaries", new { kind = "mediaType", code, name, mediaProfile }),
            HttpStatusCode.Created);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string path, HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        return await ReadExpectedJsonAsync(response, expectedStatus);
    }

    private static async Task<JsonDocument> SendJsonAsync(Task<HttpResponseMessage> request, HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await request;
        return await ReadExpectedJsonAsync(response, expectedStatus);
    }

    private static async Task<JsonDocument> ReadExpectedJsonAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        string content = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        return JsonDocument.Parse(content);
    }

    private static JsonElement FindItem(JsonDocument document, Guid itemId)
    {
        return document.RootElement.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == itemId).Clone();
    }

    private static void AssertNoItem(JsonDocument document, Guid itemId)
    {
        Assert.DoesNotContain(document.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == itemId);
    }

    private static void AssertSignal(JsonElement item, string signal)
    {
        Assert.Contains(item.GetProperty("inventorySignals").EnumerateArray(), value => value.GetString() == signal);
    }

    private static void AssertSignalsAreSorted(JsonElement item)
    {
        string[] signals = [.. item.GetProperty("inventorySignals").EnumerateArray().Select(value => value.GetString() ?? string.Empty)];
        Assert.Equal(signals.OrderBy(signal => signal, StringComparer.OrdinalIgnoreCase), signals);
    }
}
