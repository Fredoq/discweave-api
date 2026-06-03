using System.Net;
using System.Text.Json;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Api.Tests;

public sealed class ExternalMetadataTrackEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Authenticated track search normalizes query and returns release-backed candidates")]
    public async Task Authenticated_track_search_normalizes_query_and_returns_release_backed_candidates()
    {
        var provider = new FakeExternalMetadataProvider
        {
            TrackSearchResult = new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>>(
                new ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>(
                    [
                        new ExternalMetadataTrackCandidate(
                            Source("track", "249504-Qmx1ZSBNb25kYXk"),
                            "Blue Monday",
                            "A",
                            TimeSpan.FromSeconds(449),
                            ["New Order"],
                            new ExternalMetadataReleaseContext(Source("release", "249504"), "Blue Monday", 1983, ["New Order"]))
                    ],
                    1))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/external-metadata/discogs/tracks?title=%20Blue%20Monday%20&artist=%20New%20Order%20&releaseTitle=%20Blue%20Monday%20&year=1983&barcode=%205016839200371%20&catalogNumber=%20FAC%2073%20&limit=10");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Blue Monday", provider.LastTrackSearchQuery?.Title);
        Assert.Equal("New Order", provider.LastTrackSearchQuery?.Artist);
        Assert.Equal("Blue Monday", provider.LastTrackSearchQuery?.ReleaseTitle);
        Assert.Equal(1983, provider.LastTrackSearchQuery?.Year);
        Assert.Equal("5016839200371", provider.LastTrackSearchQuery?.Barcode);
        Assert.Equal("FAC 73", provider.LastTrackSearchQuery?.CatalogNumber);
        Assert.Equal(10, provider.LastTrackSearchQuery?.Limit);
        JsonElement candidate = document.RootElement.GetProperty("items")[0];
        Assert.Equal("Blue Monday", candidate.GetProperty("title").GetString());
        Assert.Equal(449, candidate.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("Blue Monday", candidate.GetProperty("release").GetProperty("title").GetString());
        Assert.Equal("Data provided by Discogs.", candidate.GetProperty("source").GetProperty("attribution").GetString());
        Assert.False(document.RootElement.TryGetProperty("collectionId", out _));
    }

    [Fact(DisplayName = "Selected track detail returns draft with credits and external source")]
    public async Task Selected_track_detail_returns_draft_with_credits_and_external_source()
    {
        var provider = new FakeExternalMetadataProvider
        {
            TrackDetailResult = new ExternalMetadataResult<ExternalMetadataTrackDetail>(
                new ExternalMetadataTrackDetail(
                    Source("track", "249504-Qmx1ZSBNb25kYXk"),
                    "Blue Monday",
                    "A",
                    TimeSpan.FromSeconds(449),
                    ["New Order"],
                    [new ExternalMetadataTrackCredit("Remixer Name", "Remix")],
                    new ExternalMetadataReleaseContext(Source("release", "249504"), "Blue Monday", 1983, ["New Order"])))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/tracks/249504-Qmx1ZSBNb25kYXk");
        string json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("249504-Qmx1ZSBNb25kYXk", provider.LastTrackLookupQuery?.ExternalId);
        JsonElement root = document.RootElement;
        Assert.Equal("Blue Monday", root.GetProperty("title").GetString());
        Assert.Equal("Remix", root.GetProperty("credits")[0].GetProperty("role").GetString());
        JsonElement draft = root.GetProperty("draft");
        Assert.Equal("Blue Monday", draft.GetProperty("title").GetString());
        Assert.Equal(449, draft.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("New Order", draft.GetProperty("artistCredits")[0].GetProperty("name").GetString());
        Assert.Equal("mainArtist", draft.GetProperty("artistCredits")[0].GetProperty("role").GetString());
        Assert.Equal("Remixer Name", draft.GetProperty("artistCredits")[1].GetProperty("name").GetString());
        Assert.Equal("Remix", draft.GetProperty("artistCredits")[1].GetProperty("role").GetString());
        Assert.Equal("track", draft.GetProperty("externalSources")[0].GetProperty("resourceType").GetString());
        Assert.DoesNotContain("collectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("marketplace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wantlist", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "External track endpoints require authentication")]
    public async Task External_track_endpoints_require_authentication()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/tracks?title=Blue");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory(DisplayName = "Track search rejects invalid query parameters")]
    [InlineData("/api/external-metadata/discogs/tracks", "external_metadata.track.criteria_required")]
    [InlineData("/api/external-metadata/discogs/tracks?title=Blue&year=invalid", "external_metadata.track.year_invalid")]
    [InlineData("/api/external-metadata/discogs/tracks?title=Blue&year=0", "external_metadata.track.year_invalid")]
    [InlineData("/api/external-metadata/discogs/tracks?title=Blue&limit=0", "external_metadata.track.limit_invalid")]
    [InlineData("/api/external-metadata/discogs/tracks?title=Blue&limit=101", "external_metadata.track.limit_invalid")]
    public async Task Track_search_rejects_invalid_query_parameters(string url, string expectedCode)
    {
        var provider = new FakeExternalMetadataProvider();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync(url);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Null(provider.LastTrackSearchQuery);
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
