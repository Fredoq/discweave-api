using System.Net;
using System.Text.Json;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Api.Tests;

public sealed class ExternalMetadataArtistEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Authenticated artist search normalizes query and returns candidate summaries")]
    public async Task Authenticated_artist_search_normalizes_query_and_returns_candidate_summaries()
    {
        var provider = new FakeExternalMetadataProvider
        {
            ArtistSearchResult = new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>>(
                new ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>(
                    [
                        new ExternalMetadataArtistCandidate(
                            Source("artist", "5876"),
                            "Arthur Baker",
                            "Producer and remixer.",
                            ["A. Baker"])
                    ],
                    1))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/artists?q=%20Arthur%20Baker%20&limit=10");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Arthur Baker", provider.LastArtistSearchQuery?.Query);
        Assert.Equal(10, provider.LastArtistSearchQuery?.Limit);
        Assert.Equal(10, document.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement candidate = document.RootElement.GetProperty("items")[0];
        Assert.Equal("Arthur Baker", candidate.GetProperty("name").GetString());
        Assert.Equal("Data provided by Discogs.", candidate.GetProperty("source").GetProperty("attribution").GetString());
        Assert.False(document.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(candidate.TryGetProperty("collectionId", out _));
    }

    [Fact(DisplayName = "Selected artist detail returns review draft without catalog writes")]
    public async Task Selected_artist_detail_returns_review_draft_without_catalog_writes()
    {
        var provider = new FakeExternalMetadataProvider
        {
            ArtistDetailResult = new ExternalMetadataResult<ExternalMetadataArtistDetail>(
                new ExternalMetadataArtistDetail(
                    Source("artist", "5876"),
                    "Arthur Baker",
                    "Producer and remixer.",
                    ["Arthur Baker III"],
                    ["Rockers Revenge"],
                    ["A. Baker"]))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/artists/5876");
        string json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("5876", provider.LastArtistLookupQuery?.ExternalId);
        JsonElement root = document.RootElement;
        Assert.Equal("Arthur Baker", root.GetProperty("name").GetString());
        Assert.Equal("Arthur Baker III", root.GetProperty("aliases")[0].GetString());
        Assert.Equal("Rockers Revenge", root.GetProperty("members")[0].GetString());
        Assert.Equal("Arthur Baker", root.GetProperty("draft").GetProperty("name").GetString());
        Assert.Equal("artist", root.GetProperty("draft").GetProperty("externalSources")[0].GetProperty("resourceType").GetString());
        Assert.DoesNotContain("collectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "External artist endpoints require authentication")]
    public async Task External_artist_endpoints_require_authentication()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/artists?q=Arthur");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory(DisplayName = "Artist search rejects invalid query parameters")]
    [InlineData("/api/external-metadata/discogs/artists", "external_metadata.artist.criteria_required")]
    [InlineData("/api/external-metadata/discogs/artists?q=Arthur&limit=0", "external_metadata.artist.limit_invalid")]
    [InlineData("/api/external-metadata/discogs/artists?q=Arthur&limit=101", "external_metadata.artist.limit_invalid")]
    public async Task Artist_search_rejects_invalid_query_parameters(string url, string expectedCode)
    {
        var provider = new FakeExternalMetadataProvider();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync(url);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Null(provider.LastArtistSearchQuery);
    }

    private static ExternalMetadataSource Source(string resourceType, string externalId)
    {
        return new ExternalMetadataSource(
            "discogs",
            resourceType,
            externalId,
            $"https://www.discogs.com/{resourceType}/{externalId}",
            "Data provided by Discogs.");
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync();

        return await JsonDocument.ParseAsync(stream);
    }
}
