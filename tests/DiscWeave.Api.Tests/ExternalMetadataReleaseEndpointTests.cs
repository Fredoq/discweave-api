using System.Net;
using System.Text.Json;
using DiscWeave.Application.ExternalMetadata;
using Microsoft.Extensions.DependencyInjection;

namespace DiscWeave.Api.Tests;

public sealed class ExternalMetadataReleaseEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact(DisplayName = "Authenticated release search normalizes query and returns candidate summaries")]
    public async Task Authenticated_release_search_normalizes_query_and_returns_candidate_summaries()
    {
        var provider = new FakeExternalMetadataProvider
        {
            ReleaseSearchResult = new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>(
                new ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>(
                    [
                        new ExternalMetadataReleaseCandidate(
                            Source("release", "249504"),
                            "New Order - Blue Monday",
                            ["New Order"],
                            1983,
                            ["Factory"],
                            ["Vinyl", "12\""],
                            "FAC 73",
                            ["5016839200371"])
                    ],
                    1))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync(
            "/api/external-metadata/discogs/releases?q=%20factory%20&artist=%20New%20Order%20&title=%20Blue%20Monday%20&year=1983&barcode=%205016839200371%20&catalogNumber=%20FAC%2073%20&limit=10");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("factory", provider.LastReleaseSearchQuery?.Query);
        Assert.Equal("New Order", provider.LastReleaseSearchQuery?.Artist);
        Assert.Equal("Blue Monday", provider.LastReleaseSearchQuery?.Title);
        Assert.Equal(1983, provider.LastReleaseSearchQuery?.Year);
        Assert.Equal("5016839200371", provider.LastReleaseSearchQuery?.Barcode);
        Assert.Equal("FAC 73", provider.LastReleaseSearchQuery?.CatalogNumber);
        Assert.Equal(10, provider.LastReleaseSearchQuery?.Limit);
        Assert.Equal(10, document.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("total").GetInt32());
        JsonElement candidate = document.RootElement.GetProperty("items")[0];
        Assert.Equal("New Order - Blue Monday", candidate.GetProperty("title").GetString());
        Assert.Equal("Data provided by Discogs.", candidate.GetProperty("source").GetProperty("attribution").GetString());
        Assert.False(document.RootElement.TryGetProperty("collectionId", out _));
        Assert.False(candidate.TryGetProperty("collectionId", out _));
    }

    [Fact(DisplayName = "Selected release detail returns review draft without persisting catalog data")]
    public async Task Selected_release_detail_returns_review_draft_without_persisting_catalog_data()
    {
        var provider = new FakeExternalMetadataProvider
        {
            ReleaseDetailResult = new ExternalMetadataResult<ExternalMetadataReleaseDetail>(
                new ExternalMetadataReleaseDetail(
                    Source("release", "249504"),
                    "Blue Monday",
                    ["New Order"],
                    1983,
                    new DateOnly(1983, 3, 7),
                    ["Factory"],
                    ["Vinyl", "12\""],
                    "single",
                    ["Electronic", "Leftfield"],
                    [new ExternalMetadataReleaseTrack("Blue Monday", "A", TimeSpan.FromSeconds(449), ["New Order"])],
                    [
                        new ExternalMetadataIdentifier("Barcode", "5016839200371"),
                        new ExternalMetadataIdentifier("Matrix / Runout", "FAC 73 A")
                    ],
                    "FAC 73",
                    [new ExternalMetadataReleaseLabel("Factory", "FAC 73")],
                    [
                        new ExternalMetadataReleaseCredit("Producer Name", "Producer", null, null),
                        new ExternalMetadataReleaseCredit("Remixer Name", "Remix", "Blue Monday", "A")
                    ]))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/releases/249504");
        string json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("249504", provider.LastReleaseLookupQuery?.ExternalId);
        JsonElement root = document.RootElement;
        Assert.Equal("Blue Monday", root.GetProperty("title").GetString());
        Assert.Equal("5016839200371", root.GetProperty("barcodes")[0].GetString());
        Assert.Equal("Producer Name", root.GetProperty("credits")[0].GetProperty("name").GetString());
        Assert.Equal("Remix", root.GetProperty("credits")[1].GetProperty("role").GetString());
        Assert.Equal("Blue Monday", root.GetProperty("credits")[1].GetProperty("trackTitle").GetString());

        JsonElement draft = root.GetProperty("draft");
        Assert.Equal("Blue Monday", draft.GetProperty("title").GetString());
        Assert.Equal("single", draft.GetProperty("type").GetString());
        Assert.Equal("Electronic", draft.GetProperty("genres")[0].GetString());
        Assert.Equal("Leftfield", draft.GetProperty("genres")[1].GetString());
        Assert.Equal(1983, draft.GetProperty("year").GetInt32());
        Assert.Equal("1983-03-07", draft.GetProperty("releaseDate").GetString());
        Assert.Equal("New Order", draft.GetProperty("artistCredits")[0].GetProperty("name").GetString());
        Assert.Equal("mainArtist", draft.GetProperty("artistCredits")[0].GetProperty("role").GetString());
        Assert.Equal("Factory", draft.GetProperty("labels")[0].GetProperty("name").GetString());
        Assert.Equal("FAC 73", draft.GetProperty("labels")[0].GetProperty("catalogNumber").GetString());
        Assert.False(draft.GetProperty("labels")[0].GetProperty("hasNoCatalogNumber").GetBoolean());
        Assert.Equal(1, draft.GetProperty("tracklist")[0].GetProperty("position").GetInt32());
        Assert.Equal(449, draft.GetProperty("tracklist")[0].GetProperty("durationSeconds").GetInt32());
        Assert.Equal("New Order", draft.GetProperty("tracklist")[0].GetProperty("artistCredits")[0].GetProperty("name").GetString());
        Assert.Equal("mainArtist", draft.GetProperty("tracklist")[0].GetProperty("artistCredits")[0].GetProperty("role").GetString());
        Assert.Equal("Remixer Name", draft.GetProperty("tracklist")[0].GetProperty("artistCredits")[1].GetProperty("name").GetString());
        Assert.Equal("Remix", draft.GetProperty("tracklist")[0].GetProperty("artistCredits")[1].GetProperty("role").GetString());
        Assert.Equal("release", draft.GetProperty("externalSources")[0].GetProperty("resourceType").GetString());
        Assert.DoesNotContain("collectionId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("marketplace", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wantlist", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "External release endpoints require authentication")]
    public async Task External_release_endpoints_require_authentication()
    {
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres);
        HttpClient client = host.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/releases?q=Factory");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory(DisplayName = "Release search rejects invalid query parameters")]
    [InlineData("/api/external-metadata/discogs/releases", "external_metadata.release.criteria_required")]
    [InlineData("/api/external-metadata/discogs/releases?q=Factory&year=invalid", "external_metadata.release.year_invalid")]
    [InlineData("/api/external-metadata/discogs/releases?q=Factory&year=0", "external_metadata.release.year_invalid")]
    [InlineData("/api/external-metadata/discogs/releases?q=Factory&limit=0", "external_metadata.release.limit_invalid")]
    [InlineData("/api/external-metadata/discogs/releases?q=Factory&limit=101", "external_metadata.release.limit_invalid")]
    public async Task Release_search_rejects_invalid_query_parameters(string url, string expectedCode)
    {
        var provider = new FakeExternalMetadataProvider();
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync(url);
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        Assert.Null(provider.LastReleaseSearchQuery);
    }

    [Fact(DisplayName = "Release search maps provider rate limits with retry after")]
    public async Task Release_search_maps_provider_rate_limits_with_retry_after()
    {
        var provider = new FakeExternalMetadataProvider
        {
            ReleaseSearchResult = new ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>>(
                new ExternalMetadataError(
                    ExternalMetadataErrorKind.RateLimited,
                    "external_metadata.rate_limited",
                    "External metadata provider rate limit was exceeded",
                    TimeSpan.FromSeconds(45)))
        };
        await using ApiTestHost host = await ApiTestHost.CreateAsync(postgres, services => services.AddSingleton<IExternalMetadataProvider>(provider));
        HttpClient client = await host.CreateAuthenticatedClientAsync();

        using HttpResponseMessage response = await client.GetAsync("/api/external-metadata/discogs/releases?q=Factory");
        using JsonDocument document = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("45", response.Headers.RetryAfter?.Delta?.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("external_metadata.rate_limited", document.RootElement.GetProperty("code").GetString());
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
