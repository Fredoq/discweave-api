using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class SettingsDictionaryEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public SettingsDictionaryEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Dictionary endpoints expose seeded collection defaults without collection identifiers")]
    public async Task Dictionary_endpoints_expose_seeded_collection_defaults_without_collection_identifiers()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/settings/dictionaries");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        JsonElement items = document.RootElement.GetProperty("items");
        Assert.Contains(items.EnumerateArray(), entry =>
            entry.GetProperty("kind").GetString() == "releaseType" &&
            entry.GetProperty("code").GetString() == "album" &&
            entry.GetProperty("name").GetString() == "Album" &&
            entry.GetProperty("isBuiltin").GetBoolean());
        Assert.Contains(items.EnumerateArray(), entry =>
            entry.GetProperty("kind").GetString() == "creditRole" &&
            entry.GetProperty("code").GetString() == "mainArtist" &&
            entry.GetProperty("isProtected").GetBoolean());
        Assert.Contains(items.EnumerateArray(), entry =>
            entry.GetProperty("kind").GetString() == "mediaType" &&
            entry.GetProperty("code").GetString() == "vinyl" &&
            entry.GetProperty("mediaProfile").GetString() == "vinyl");
        Assert.DoesNotContain(items.EnumerateArray(), entry => entry.TryGetProperty("collectionId", out _));
    }

    [Fact(DisplayName = "Catalog writes require active dictionary entries")]
    public async Task Catalog_writes_require_active_dictionary_entries()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createEntryResponse = await client.PostAsJsonAsync(
            "/api/settings/dictionaries",
            new { kind = "releaseType", code = "demoTape", name = "Demo tape", sortOrder = 90, isActive = true });
        using JsonDocument createEntryDocument = await ReadJsonAsync(createEntryResponse);
        Guid entryId = createEntryDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage createReleaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new { title = "Basement Sessions", type = "demoTape", isVariousArtists = true, genres = Array.Empty<string>(), tags = Array.Empty<string>() });
        using JsonDocument createReleaseDocument = await ReadJsonAsync(createReleaseResponse);

        using HttpResponseMessage deactivateResponse = await client.PutAsJsonAsync(
            $"/api/settings/dictionaries/{entryId}",
            new { name = "Demo tape", sortOrder = 90, isActive = false });

        using HttpResponseMessage inactiveReleaseResponse = await client.PostAsJsonAsync(
            "/api/releases",
            new { title = "Second Basement Sessions", type = "demoTape", isVariousArtists = true, genres = Array.Empty<string>(), tags = Array.Empty<string>() });
        using JsonDocument inactiveReleaseDocument = await ReadJsonAsync(inactiveReleaseResponse);

        Assert.Equal(HttpStatusCode.Created, createEntryResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, createReleaseResponse.StatusCode);
        Assert.Equal("demoTape", createReleaseDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inactiveReleaseResponse.StatusCode);
        Assert.Equal("release.type_invalid", inactiveReleaseDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Dictionary endpoints filter by kind and validate kind codes")]
    public async Task Dictionary_endpoints_filter_by_kind_and_validate_kind_codes()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage genreResponse = await client.GetAsync("/api/settings/dictionaries?kind=genre");
        using JsonDocument genreDocument = await ReadJsonAsync(genreResponse);
        using HttpResponseMessage invalidKindResponse = await client.GetAsync("/api/settings/dictionaries?kind=labelType");
        using JsonDocument invalidKindDocument = await ReadJsonAsync(invalidKindResponse);

        Assert.Equal(HttpStatusCode.OK, genreResponse.StatusCode);
        JsonElement genreItems = genreDocument.RootElement.GetProperty("items");
        Assert.True(genreDocument.RootElement.GetProperty("total").GetInt32() > 0);
        Assert.All(genreItems.EnumerateArray(), entry => Assert.Equal("genre", entry.GetProperty("kind").GetString()));
        Assert.Contains(genreItems.EnumerateArray(), entry => entry.GetProperty("code").GetString() == "Electronic");
        Assert.Equal(HttpStatusCode.BadRequest, invalidKindResponse.StatusCode);
        Assert.Equal("dictionary_entry.kind_invalid", invalidKindDocument.RootElement.GetProperty("code").GetString());
    }

    [Fact(DisplayName = "Dictionary entries can be updated and unused entries require delete confirmation")]
    public async Task Dictionary_entries_can_be_updated_and_unused_entries_require_delete_confirmation()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/settings/dictionaries",
            new { kind = "mediaType", code = "lathe", name = "Lathe cut", sortOrder = 95, mediaProfile = "vinyl" });
        using JsonDocument createDocument = await ReadJsonAsync(createResponse);
        Guid entryId = createDocument.RootElement.GetProperty("id").GetGuid();

        using HttpResponseMessage duplicateResponse = await client.PostAsJsonAsync(
            "/api/settings/dictionaries",
            new { kind = "mediaType", code = "lathe", name = "Lathe duplicate", mediaProfile = "vinyl" });
        using JsonDocument duplicateDocument = await ReadJsonAsync(duplicateResponse);

        using HttpResponseMessage updateResponse = await client.PutAsJsonAsync(
            $"/api/settings/dictionaries/{entryId}",
            new { name = "Lathe", sortOrder = 96, isActive = false, mediaProfile = "other" });
        using JsonDocument updateDocument = await ReadJsonAsync(updateResponse);

        using HttpResponseMessage deleteWithoutConfirmationResponse = await client.DeleteAsync($"/api/settings/dictionaries/{entryId}");
        using JsonDocument deleteWithoutConfirmationDocument = await ReadJsonAsync(deleteWithoutConfirmationResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/settings/dictionaries/{entryId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"dictionary-entry:{entryId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal("vinyl", createDocument.RootElement.GetProperty("mediaProfile").GetString());
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal("dictionary_entry.code_conflict", duplicateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal("Lathe", updateDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(96, updateDocument.RootElement.GetProperty("sortOrder").GetInt32());
        Assert.False(updateDocument.RootElement.GetProperty("isActive").GetBoolean());
        Assert.Equal("other", updateDocument.RootElement.GetProperty("mediaProfile").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, deleteWithoutConfirmationResponse.StatusCode);
        Assert.Equal("delete.confirmation_required", deleteWithoutConfirmationDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact(DisplayName = "Protected built-in dictionary entries cannot be disabled or deleted")]
    public async Task Protected_builtin_dictionary_entries_cannot_be_disabled_or_deleted()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid mainArtistId = await FindDictionaryEntryIdAsync(client, "creditRole", "mainArtist");

        using HttpResponseMessage deactivateResponse = await client.PutAsJsonAsync(
            $"/api/settings/dictionaries/{mainArtistId}",
            new { name = "Main artist", sortOrder = 10, isActive = false });
        using JsonDocument deactivateDocument = await ReadJsonAsync(deactivateResponse);

        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/settings/dictionaries/{mainArtistId}");
        deleteRequest.Headers.Add("X-DiscWeave-Confirm-Delete", $"dictionary-entry:{mainArtistId}");
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);
        using JsonDocument deleteDocument = await ReadJsonAsync(deleteResponse);

        Assert.Equal(HttpStatusCode.BadRequest, deactivateResponse.StatusCode);
        Assert.Equal("dictionary_entry.protected", deactivateDocument.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, deleteResponse.StatusCode);
        Assert.Equal("dictionary_entry.protected", deleteDocument.RootElement.GetProperty("code").GetString());
    }

    private static async Task<Guid> FindDictionaryEntryIdAsync(HttpClient client, string kind, string code)
    {
        using HttpResponseMessage response = await client.GetAsync($"/api/settings/dictionaries?kind={kind}");
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return document.RootElement.GetProperty("items")
            .EnumerateArray()
            .Single(entry => entry.GetProperty("code").GetString() == code)
            .GetProperty("id")
            .GetGuid();
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
}
