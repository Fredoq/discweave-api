using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DiscWeave.Api.Tests;

public sealed class SearchCollectorViewEndpointTests : IClassFixture<PostgresFixture>
{
    private static readonly string[] EmptyStrings = [];
    private readonly PostgresFixture _postgres;

    public SearchCollectorViewEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [Theory(DisplayName = "Search saved collector views expose remixes productions and labels")]
    [InlineData("remixes", "track")]
    [InlineData("productions", "release")]
    [InlineData("labels", "label")]
    public async Task Search_saved_collector_views_expose_remixes_productions_and_labels(string savedView, string expectedType)
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(_postgres);
        HttpClient client = await host.CreateAuthenticatedClientAsync();
        Guid producerId = await CreateArtistAsync(client, "Producer Person");
        Guid remixerId = await CreateArtistAsync(client, "Remixer Person");
        Guid labelId = await CreateLabelAsync(client, "Navigation Label");
        Guid releaseId = await CreateReleaseAsync(client, "Produced Release", labelId);
        Guid trackId = await CreateTrackAsync(client, "Remixed Track");
        _ = await CreateCreditAsync(client, producerId, "release", releaseId, "producer");
        _ = await CreateCreditAsync(client, remixerId, "track", trackId, "remixer");

        using HttpResponseMessage response = await client.GetAsync($"/api/search?savedView={savedView}&limit=20&offset=0");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);
        JsonElement[] items = [.. document.RootElement.GetProperty("items").EnumerateArray()];
        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.Equal(expectedType, item.GetProperty("type").GetString()));
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

    private static async Task<Guid> CreateReleaseAsync(HttpClient client, string title, Guid labelId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/releases",
            new { title, type = "standalone", isVariousArtists = true, labelId, year = 1983, genres = EmptyStrings, tags = EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateTrackAsync(HttpClient client, string title)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/tracks", new { title, genres = EmptyStrings, tags = EmptyStrings });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateCreditAsync(HttpClient client, Guid contributorArtistId, string targetType, Guid targetId, string role)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/credits",
            new { contributorArtistId, targetType, targetId, roles = new[] { role } });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using JsonDocument document = await ReadJsonAsync(response);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
    }
}
