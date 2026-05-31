using System.Net;
using DiscWeave.Application.ExternalMetadata;
using DiscWeave.Infrastructure.ExternalMetadata.Discogs;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Tests;

public sealed class DiscogsExternalMetadataProviderTests
{
    [Fact(DisplayName = "Disabled Discogs provider returns a deterministic error without HTTP")]
    public async Task Disabled_Discogs_provider_returns_a_deterministic_error_without_Http()
    {
        RecordingHttpMessageHandler handler = new(_ => throw new InvalidOperationException("HTTP must not be called"));
        DiscogsExternalMetadataProvider provider = CreateProvider(handler, new DiscogsOptions { Enabled = false });

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Title: "Blue Monday"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.Disabled, result.Error.Kind);
        Assert.Equal("external_metadata.disabled", result.Error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact(DisplayName = "Enabled Discogs provider requires a token before HTTP")]
    public async Task Enabled_Discogs_provider_requires_a_token_before_Http()
    {
        RecordingHttpMessageHandler handler = new(_ => throw new InvalidOperationException("HTTP must not be called"));
        DiscogsExternalMetadataProvider provider = CreateProvider(handler, new DiscogsOptions { Enabled = true, AccessToken = " " });

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Title: "Blue Monday"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.NotConfigured, result.Error.Kind);
        Assert.Equal("external_metadata.not_configured", result.Error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact(DisplayName = "Release search sends Discogs query parameters and maps candidate summaries")]
    public async Task Release_search_sends_Discogs_query_parameters_and_maps_candidate_summaries()
    {
        RecordingHttpMessageHandler handler = JsonHandler(
            // lang=json
            """
            {
              "pagination": { "items": 1 },
              "results": [
                {
                  "type": "release",
                  "id": 249504,
                  "title": "New Order - Blue Monday",
                  "year": 1983,
                  "label": [ "Factory" ],
                  "format": [ "Vinyl", "12\"" ],
                  "catno": "FAC 73",
                  "barcode": [ "5016839200371" ],
                  "uri": "/release/249504-New-Order-Blue-Monday"
                }
              ]
            }
            """);
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(
                new ExternalMetadataReleaseSearchQuery(Artist: "New Order", Title: "Blue Monday", Year: 1983, CatalogNumber: "FAC 73"),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/database/search", request.RequestUri?.AbsolutePath);
        Assert.Contains("type=release", request.RequestUri?.Query);
        Assert.Contains("artist=New%20Order", request.RequestUri?.Query);
        Assert.Contains("release_title=Blue%20Monday", request.RequestUri?.Query);
        Assert.Contains("year=1983", request.RequestUri?.Query);
        Assert.Contains("catno=FAC%2073", request.RequestUri?.Query);
        Assert.Equal("DiscWeave.Tests/1.0", request.Headers.UserAgent.ToString());
        Assert.Equal("Discogs token=test-token", request.Headers.Authorization?.ToString());

        ExternalMetadataReleaseCandidate candidate = Assert.Single(result.Value.Items);
        Assert.Equal("discogs", candidate.Source.ProviderName);
        Assert.Equal("release", candidate.Source.ResourceType);
        Assert.Equal("249504", candidate.Source.ExternalId);
        Assert.Equal("https://www.discogs.com/release/249504-New-Order-Blue-Monday", candidate.Source.SourceUrl);
        Assert.Equal("Data provided by Discogs.", candidate.Source.Attribution);
        Assert.Equal("New Order - Blue Monday", candidate.Title);
        Assert.Equal(1983, candidate.Year);
        Assert.Contains("Factory", candidate.Labels);
        Assert.Contains("Vinyl", candidate.Formats);
        Assert.Contains("5016839200371", candidate.Barcodes);
    }

    [Fact(DisplayName = "Artist detail maps Discogs aliases and members")]
    public async Task Artist_detail_maps_Discogs_aliases_and_members()
    {
        RecordingHttpMessageHandler handler = JsonHandler(
            // lang=json
            """
            {
              "id": 5876,
              "name": "Arthur Baker",
              "profile": "Producer and remixer.",
              "uri": "/artist/5876-Arthur-Baker",
              "aliases": [ { "name": "Arthur Baker III" } ],
              "members": [ { "name": "Rockers Revenge" } ],
              "namevariations": [ "A. Baker" ]
            }
            """);
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataArtistDetail> result =
            await provider.GetArtistAsync(new ExternalMetadataLookupQuery("5876"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal("/artists/5876", request.RequestUri?.AbsolutePath);
        Assert.Equal("Arthur Baker", result.Value.Name);
        Assert.Equal("Producer and remixer.", result.Value.Profile);
        Assert.Contains("Arthur Baker III", result.Value.Aliases);
        Assert.Contains("Rockers Revenge", result.Value.Members);
        Assert.Contains("A. Baker", result.Value.NameVariations);
    }

    [Fact(DisplayName = "Release-backed track search uses release context and maps track candidates")]
    public async Task Release_backed_track_search_uses_release_context_and_maps_track_candidates()
    {
        RecordingHttpMessageHandler handler = JsonHandler(
            // lang=json
            """
            {
              "pagination": { "items": 1 },
              "results": [
                {
                  "type": "release",
                  "id": 249504,
                  "title": "New Order - Blue Monday",
                  "year": 1983,
                  "label": [ "Factory" ],
                  "format": [ "Vinyl", "12\"" ],
                  "catno": "FAC 73",
                  "uri": "/release/249504-New-Order-Blue-Monday"
                }
              ]
            }
            """);
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataTrackCandidate>> result =
            await provider.SearchTracksAsync(
                new ExternalMetadataTrackSearchQuery(Title: "Blue Monday", Artist: "New Order", ReleaseTitle: "Blue Monday"),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Contains("track=Blue%20Monday", request.RequestUri?.Query);
        Assert.Contains("artist=New%20Order", request.RequestUri?.Query);
        Assert.Contains("release_title=Blue%20Monday", request.RequestUri?.Query);
        ExternalMetadataTrackCandidate candidate = Assert.Single(result.Value.Items);
        Assert.Equal("Blue Monday", candidate.Title);
        Assert.Equal("New Order - Blue Monday", candidate.Release.Title);
        Assert.Equal("249504", candidate.Release.Source.ExternalId);
    }

    [Fact(DisplayName = "Discogs rate limits map to deterministic provider errors with retry-after")]
    public async Task Discogs_rate_limits_map_to_deterministic_provider_errors_with_retry_after()
    {
        RecordingHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal("external_metadata.rate_limited", result.Error.Code);
        Assert.Equal(TimeSpan.FromSeconds(30), result.Error.RetryAfter);
    }

    [Theory(DisplayName = "Discogs provider failures map to safe deterministic errors")]
    [InlineData(HttpStatusCode.Unauthorized, ExternalMetadataErrorKind.Unauthorized, "external_metadata.unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, ExternalMetadataErrorKind.Unauthorized, "external_metadata.unauthorized")]
    [InlineData(HttpStatusCode.BadGateway, ExternalMetadataErrorKind.Unavailable, "external_metadata.unavailable")]
    [InlineData(HttpStatusCode.ServiceUnavailable, ExternalMetadataErrorKind.Unavailable, "external_metadata.unavailable")]
    public async Task Discogs_provider_failures_map_to_safe_deterministic_errors(
        HttpStatusCode statusCode,
        ExternalMetadataErrorKind expectedKind,
        string expectedCode)
    {
        RecordingHttpMessageHandler handler = new(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("secret upstream body")
        });
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedKind, result.Error.Kind);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.DoesNotContain("secret", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Malformed Discogs JSON maps to invalid response")]
    public async Task Malformed_Discogs_Json_maps_to_invalid_response()
    {
        RecordingHttpMessageHandler handler = JsonHandler("{ invalid json");
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.InvalidResponse, result.Error.Kind);
        Assert.Equal("external_metadata.invalid_response", result.Error.Code);
    }

    [Fact(DisplayName = "HTTP cancellation by timeout maps to timeout while caller cancellation is preserved")]
    public async Task Http_cancellation_by_timeout_maps_to_timeout_while_caller_cancellation_is_preserved()
    {
        RecordingHttpMessageHandler timeoutHandler = new(_ => throw new TaskCanceledException("handler timeout"));
        DiscogsExternalMetadataProvider timeoutProvider = CreateProvider(timeoutHandler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> timeoutResult =
            await timeoutProvider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        using CancellationTokenSource callerCancellation = new();
        await callerCancellation.CancelAsync();
        DiscogsExternalMetadataProvider cancelledProvider = CreateProvider(JsonHandler("{}"));

        Assert.Equal(ExternalMetadataErrorKind.Timeout, timeoutResult.Error.Kind);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancelledProvider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), callerCancellation.Token));
    }

    [Fact(DisplayName = "Provider errors never leak the configured Discogs token")]
    public async Task Provider_errors_never_leak_the_configured_Discogs_token()
    {
        const string token = "super-secret-token";
        RecordingHttpMessageHandler handler = new(_ => throw new HttpRequestException($"provider rejected {token}"));
        DiscogsExternalMetadataProvider provider = CreateProvider(handler, new DiscogsOptions
        {
            Enabled = true,
            AccessToken = token,
            UserAgent = "DiscWeave.Tests/1.0",
            BaseUrl = "https://api.discogs.test",
            TimeoutSeconds = 10
        });

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.Unavailable, result.Error.Kind);
        Assert.DoesNotContain(token, result.Error.Message, StringComparison.Ordinal);
    }

    private static DiscogsExternalMetadataProvider CreateProvider(
        RecordingHttpMessageHandler handler,
        DiscogsOptions? options = null)
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.discogs.test")
        };

        return new DiscogsExternalMetadataProvider(
            httpClient,
            Options.Create(options ?? new DiscogsOptions
            {
                Enabled = true,
                AccessToken = "test-token",
                UserAgent = "DiscWeave.Tests/1.0",
                BaseUrl = "https://api.discogs.test",
                TimeoutSeconds = 10
            }));
    }

    private static RecordingHttpMessageHandler JsonHandler(string content)
    {
        return new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
    }

}
