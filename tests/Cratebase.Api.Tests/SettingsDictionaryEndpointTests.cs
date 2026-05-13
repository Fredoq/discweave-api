using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cratebase.Api.Tests;

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
