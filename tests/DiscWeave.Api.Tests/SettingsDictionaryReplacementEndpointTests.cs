using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class SettingsDictionaryReplacementEndpointTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _postgres;

    public SettingsDictionaryReplacementEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Fact(DisplayName = "Used dictionary entries can be replaced across catalog data")]
    public async Task Used_dictionary_entries_can_be_replaced_across_catalog_data()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        Guid releaseTypeId = await CreateDictionaryEntryAsync(client, new { kind = "releaseType", code = "demoTape", name = "Demo tape" });
        Guid creditRoleId = await CreateDictionaryEntryAsync(client, new { kind = "creditRole", code = "djMix", name = "DJ mix" });
        Guid genreId = await CreateDictionaryEntryAsync(client, new { kind = "genre", code = "Electroclash", name = "Electroclash" });
        Guid mediaTypeId = await CreateDictionaryEntryAsync(client, new { kind = "mediaType", code = "lathe", name = "Lathe cut", mediaProfile = "vinyl" });
        Guid artistRelationTypeId = await CreateDictionaryEntryAsync(client, new { kind = "artistRelationType", code = "mentorOf", name = "Mentor of" });
        Guid trackRelationTypeId = await CreateDictionaryEntryAsync(client, new { kind = "trackRelationType", code = "dubOf", name = "Dub of" });

        Guid artistId = await CreateArtistAsync(client, "Dictionary Artist");
        Guid relatedArtistId = await CreateArtistAsync(client, "Dictionary Mentor");
        Guid releaseId = await CreateReleaseAsync(client, "Dictionary Release", "demoTape", ["Electroclash"]);
        Guid trackId = await CreateTrackAsync(client, "Dictionary Dub", ["Electroclash"]);
        Guid targetTrackId = await CreateTrackAsync(client, "Dictionary Original", []);
        Guid creditId = await CreateCreditAsync(client, artistId, releaseId, "djMix");
        Guid ownedItemId = await CreateOwnedItemAsync(client, releaseId, "lathe");
        Guid artistRelationId = await CreateArtistRelationAsync(client, artistId, relatedArtistId, "mentorOf");
        Guid trackRelationId = await CreateTrackRelationAsync(client, trackId, targetTrackId, "dubOf");

        foreach (Guid entryId in new[] { releaseTypeId, creditRoleId, genreId, mediaTypeId, artistRelationTypeId, trackRelationTypeId })
        {
            await AssertUsedEntryCannotBeDeletedAsync(client, entryId);
        }

        await ReplaceDictionaryEntryAsync(client, releaseTypeId, "standalone");
        await ReplaceDictionaryEntryAsync(client, creditRoleId, "producer");
        await ReplaceDictionaryEntryAsync(client, genreId, "Ambient");
        await ReplaceDictionaryEntryAsync(client, mediaTypeId, "vinyl");
        await ReplaceDictionaryEntryAsync(client, artistRelationTypeId, "collaboration");
        await ReplaceDictionaryEntryAsync(client, trackRelationTypeId, "remixOf");

        using JsonDocument releaseDocument = await GetJsonAsync(client, $"/api/releases/{releaseId}");
        using JsonDocument trackDocument = await GetJsonAsync(client, $"/api/tracks/{trackId}");
        using JsonDocument creditDocument = await GetJsonAsync(client, $"/api/credits/{creditId}");
        using JsonDocument ownedItemDocument = await GetJsonAsync(client, $"/api/owned-items/{ownedItemId}");
        using JsonDocument artistRelationDocument = await GetJsonAsync(client, $"/api/artist-relations/{artistRelationId}");
        using JsonDocument trackRelationDocument = await GetJsonAsync(client, $"/api/track-relations/{trackRelationId}");

        Assert.Equal("standalone", releaseDocument.RootElement.GetProperty("type").GetString());
        AssertStringArray(["Ambient"], releaseDocument.RootElement.GetProperty("genres"));
        AssertStringArray(["Ambient"], trackDocument.RootElement.GetProperty("genres"));
        Assert.Equal("producer", creditDocument.RootElement.GetProperty("role").GetString());
        Assert.Equal("vinyl", ownedItemDocument.RootElement.GetProperty("medium").GetProperty("type").GetString());
        Assert.Equal("collaboration", artistRelationDocument.RootElement.GetProperty("type").GetString());
        Assert.Equal("remixOf", trackRelationDocument.RootElement.GetProperty("type").GetString());

        await AssertDictionaryEntryRemovedAsync(client, "releaseType", "demoTape");
        await AssertDictionaryEntryRemovedAsync(client, "creditRole", "djMix");
        await AssertDictionaryEntryRemovedAsync(client, "genre", "Electroclash");
        await AssertDictionaryEntryRemovedAsync(client, "mediaType", "lathe");
        await AssertDictionaryEntryRemovedAsync(client, "artistRelationType", "mentorOf");
        await AssertDictionaryEntryRemovedAsync(client, "trackRelationType", "dubOf");
    }

    private static async Task<Guid> CreateDictionaryEntryAsync(HttpClient client, object request)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/settings/dictionaries", request);
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artists", new { type = "person", name });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, string type, IReadOnlyList<string> genres)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type, isVariousArtists = true, genres, tags = Array.Empty<string>() });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title, IReadOnlyList<string> genres)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/tracks",
            new { title, genres, tags = Array.Empty<string>() });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, Guid releaseId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId, targetType = "release", targetId = releaseId, roles = new[] { role } });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateOwnedItemAsync(HttpClient client, Guid releaseId, string mediumType)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/owned-items",
            new
            {
                targetType = "release",
                targetId = releaseId,
                status = "owned",
                medium = new { type = mediumType, description = "7 inch" }
            });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateArtistRelationAsync(HttpClient client, Guid sourceArtistId, Guid targetArtistId, string type)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/artist-relations", new { sourceArtistId, targetArtistId, type });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackRelationAsync(HttpClient client, Guid sourceTrackId, Guid targetTrackId, string type)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/track-relations", new { sourceTrackId, targetTrackId, type });
        using JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task AssertUsedEntryCannotBeDeletedAsync(HttpClient client, Guid entryId)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, $"/api/settings/dictionaries/{entryId}");
        request.Headers.Add("X-DiscWeave-Confirm-Delete", $"dictionary-entry:{entryId}");
        using HttpResponseMessage response = await client.SendAsync(request);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("dictionary_entry.in_use", document.RootElement.GetProperty("code").GetString());
    }

    private static async Task ReplaceDictionaryEntryAsync(HttpClient client, Guid entryId, string replacementCode)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync($"/api/settings/dictionaries/{entryId}/replace", new { replacementCode });
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(replacementCode, document.RootElement.GetProperty("code").GetString());
    }

    private static async Task AssertDictionaryEntryRemovedAsync(HttpClient client, string kind, string code)
    {
        using JsonDocument document = await GetJsonAsync(client, $"/api/settings/dictionaries?kind={kind}");

        Assert.DoesNotContain(
            document.RootElement.GetProperty("items").EnumerateArray(),
            entry => entry.GetProperty("code").GetString() == code);
    }

    private static async Task<JsonDocument> GetJsonAsync(HttpClient client, string requestUri)
    {
        using HttpResponseMessage response = await client.GetAsync(requestUri);
        JsonDocument document = await ReadJsonAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return document;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }

    private static void AssertStringArray(IReadOnlyList<string> expected, JsonElement actual)
    {
        Assert.Equal(expected, [.. actual.EnumerateArray().Select(item => item.GetString() ?? string.Empty)]);
    }
}
