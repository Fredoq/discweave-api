using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

public sealed class ManualCatalogApiContractTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public ManualCatalogApiContractTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Manual catalog endpoints accept incomplete meaningful records without collection identifiers")]
    public async Task Manual_catalog_endpoints_accept_incomplete_meaningful_records_without_collection_identifiers()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using JsonDocument artist = await SendJsonAsync(client.PostAsJsonAsync(
            "/api/artists",
            new { type = "person", name = "Mystery Contributor" }),
            HttpStatusCode.Created);
        using JsonDocument label = await SendJsonAsync(client.PostAsJsonAsync(
            "/api/labels",
            new { name = "Private Press" }),
            HttpStatusCode.Created);
        using JsonDocument release = await SendJsonAsync(client.PostAsJsonAsync(
            "/api/releases",
            new { title = "Untitled Acetate", isVariousArtists = true }),
            HttpStatusCode.Created);
        using JsonDocument track = await SendJsonAsync(client.PostAsJsonAsync(
            "/api/tracks",
            new { title = "Unknown Take" }),
            HttpStatusCode.Created);

        AssertNoCollectionIdentifiers(artist.RootElement);
        AssertNoCollectionIdentifiers(label.RootElement);
        AssertNoCollectionIdentifiers(release.RootElement);
        AssertNoCollectionIdentifiers(track.RootElement);
        Assert.Equal("unknown", release.RootElement.GetProperty("type").GetString());
        Assert.True(release.RootElement.GetProperty("labelId").ValueKind is JsonValueKind.Null);
        Assert.True(release.RootElement.GetProperty("year").ValueKind is JsonValueKind.Null);
        Assert.Equal(0, release.RootElement.GetProperty("tracklist").GetArrayLength());
        Assert.True(track.RootElement.GetProperty("durationSeconds").ValueKind is JsonValueKind.Null);
    }

    [Fact(DisplayName = "Release update replaces credits labels and tracklist when manual entry sends them")]
    public async Task Release_update_replaces_credits_labels_and_tracklist_when_manual_entry_sends_them()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid firstArtistId = await CreateArtistAsync(client, "First Artist");
        Guid secondArtistId = await CreateArtistAsync(client, "Second Artist");
        Guid firstLabelId = await CreateLabelAsync(client, "First Label");
        Guid secondLabelId = await CreateLabelAsync(client, "Second Label");

        using JsonDocument created = await SendJsonAsync(client.PostAsJsonAsync(
            "/api/releases",
            new
            {
                title = "Mutable Release",
                type = "album",
                isVariousArtists = false,
                artistCredits = new[] { new { artistId = firstArtistId, role = "mainArtist" } },
                labels = new[] { new { labelId = firstLabelId, catalogNumber = "ONE", hasNoCatalogNumber = false } },
                tracklist = new[] { new { title = "First Track", position = 1, durationSeconds = 60 } }
            }),
            HttpStatusCode.Created);
        Guid releaseId = created.RootElement.GetProperty("id").GetGuid();

        using JsonDocument updated = await SendJsonAsync(client.PutAsJsonAsync(
            $"/api/releases/{releaseId}",
            new
            {
                title = "Mutable Release Revised",
                type = "standalone",
                isVariousArtists = false,
                artistCredits = new[] { new { artistId = secondArtistId, role = "mainArtist" } },
                labels = new[] { new { labelId = secondLabelId, catalogNumber = "TWO", hasNoCatalogNumber = false } },
                tracklist = new[] { new { title = "Second Track", position = 1, durationSeconds = 120 } }
            }),
            HttpStatusCode.OK);

        JsonElement root = updated.RootElement;
        AssertNoCollectionIdentifiers(root);
        Assert.Equal("Mutable Release Revised", root.GetProperty("title").GetString());
        Assert.Equal(secondArtistId, root.GetProperty("artistCredits")[0].GetProperty("artistId").GetGuid());
        Assert.Equal(secondLabelId, root.GetProperty("labels")[0].GetProperty("labelId").GetGuid());
        Assert.Equal("Second Track", root.GetProperty("tracklist")[0].GetProperty("title").GetString());
        Assert.Equal(120, root.GetProperty("tracklist")[0].GetProperty("durationSeconds").GetInt32());
    }

    [Fact(DisplayName = "Owned item update can replace target medium and holding details")]
    public async Task Owned_item_update_can_replace_target_medium_and_holding_details()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Original Release");
        Guid trackId = await CreateTrackAsync(client, "Linked Track");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId);

        using JsonDocument updated = await SendJsonAsync(client.PutAsJsonAsync(
            $"/api/owned-items/{ownedItemId}",
            new
            {
                targetType = "track",
                targetId = trackId,
                status = "needsDigitization",
                medium = new { type = "cassette", description = "Chrome tape" },
                condition = "veryGood",
                storageLocation = "Transfer shelf"
            }),
            HttpStatusCode.OK);

        JsonElement root = updated.RootElement;
        AssertNoCollectionIdentifiers(root);
        Assert.Equal("track", root.GetProperty("targetType").GetString());
        Assert.Equal(trackId, root.GetProperty("targetId").GetGuid());
        Assert.Equal("needsDigitization", root.GetProperty("status").GetString());
        Assert.Equal("cassette", root.GetProperty("medium").GetProperty("type").GetString());
        Assert.Equal("Chrome tape", root.GetProperty("medium").GetProperty("description").GetString());
        Assert.Equal("veryGood", root.GetProperty("condition").GetString());
        Assert.Equal("Transfer shelf", root.GetProperty("storageLocation").GetString());
    }

    [Fact(DisplayName = "Owned item update preserves target and medium when omitted")]
    public async Task Owned_item_update_preserves_target_and_medium_when_omitted()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Preserved Release");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId);

        using JsonDocument updated = await SendJsonAsync(client.PutAsJsonAsync(
            $"/api/owned-items/{ownedItemId}",
            new { status = "sold", condition = "good", storageLocation = "Sold archive" }),
            HttpStatusCode.OK);

        JsonElement root = updated.RootElement;
        Assert.Equal("release", root.GetProperty("targetType").GetString());
        Assert.Equal(releaseId, root.GetProperty("targetId").GetGuid());
        Assert.Equal("vinyl", root.GetProperty("medium").GetProperty("type").GetString());
        Assert.Equal("sold", root.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Owned item update rejects partial target shapes")]
    public async Task Owned_item_update_rejects_partial_target_shapes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid releaseId = await CreateReleaseAsync(client, "Target Shape Release");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId);

        using JsonDocument response = await SendJsonAsync(client.PutAsJsonAsync(
            $"/api/owned-items/{ownedItemId}",
            new { targetType = "track", status = "owned" }),
            HttpStatusCode.BadRequest);

        Assert.Equal("owned_item.target_shape_invalid", response.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/artists", new { type = "person", name }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateLabelAsync(HttpClient client, string name)
    {
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/labels", new { name }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title)
    {
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/releases", new { title, isVariousArtists = true }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using JsonDocument document = await SendJsonAsync(client.PostAsJsonAsync("/api/tracks", new { title }), HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId)
    {
        using JsonDocument document = await SendJsonAsync(
            client.PostAsJsonAsync(
                "/api/owned-items",
                new
                {
                    targetType = "release",
                    targetId = releaseId,
                    status = "owned",
                    medium = new { type = "vinyl", description = "LP" }
                }),
            HttpStatusCode.Created);
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> SendJsonAsync(Task<HttpResponseMessage> request, HttpStatusCode expectedStatus)
    {
        using HttpResponseMessage response = await request;
        string content = await response.Content.ReadAsStringAsync();
        Assert.Equal(expectedStatus, response.StatusCode);
        return JsonDocument.Parse(content);
    }

    private static void AssertNoCollectionIdentifiers(JsonElement element)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            Assert.DoesNotMatch("collectionId|defaultCollectionId", property.Name);
            if (property.Value.ValueKind is JsonValueKind.Object)
            {
                AssertNoCollectionIdentifiers(property.Value);
            }
            else if (property.Value.ValueKind is JsonValueKind.Array)
            {
                foreach (JsonElement child in property.Value.EnumerateArray().Where(child => child.ValueKind is JsonValueKind.Object))
                {
                    AssertNoCollectionIdentifiers(child);
                }
            }
        }
    }
}
