using System.Net;
using DiscWeave.Application.ExternalMetadata;
using DiscWeave.Infrastructure.ExternalMetadata.Discogs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Tests;

public sealed class DiscogsProviderHardeningTests
{
    [Fact(DisplayName = "Discogs provider retries one transient server failure before succeeding")]
    public async Task Discogs_provider_retries_one_transient_server_failure_before_succeeding()
    {
        int attempts = 0;
        RecordingHttpMessageHandler handler = new(_ =>
        {
            attempts++;
            return attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : JsonResponse(
                    // lang=json
                    """
                    { "pagination": { "items": 0 }, "results": [] }
                    """);
        });
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact(DisplayName = "Discogs provider does not retry rate limited responses")]
    public async Task Discogs_provider_does_not_retry_rate_limited_responses()
    {
        RecordingHttpMessageHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return response;
        });
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExternalMetadataErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(42), result.Error.RetryAfter);
        _ = Assert.Single(handler.Requests);
    }

    [Fact(DisplayName = "Discogs provider logs deterministic metadata without leaking upstream secrets")]
    public async Task Discogs_provider_logs_deterministic_metadata_without_leaking_upstream_secrets()
    {
        const string accessToken = "discogs-token-should-not-appear";
        CapturingLogger logger = new();
        RecordingHttpMessageHandler handler = new(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent(
                    // lang=json
                    """
                    {
                      "message": "discogs-token-should-not-appear",
                      "collection_id": "private-collection",
                      "image": "https://img.discogs.com/private.jpg",
                      "marketplace": "seller data",
                      "wantlist": "restricted data",
                      "stackTrace": "internal exception text"
                    }
                    """)
            });
        DiscogsExternalMetadataProvider provider = CreateProvider(handler, logger, accessToken);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataReleaseCandidate>> result =
            await provider.SearchReleasesAsync(new ExternalMetadataReleaseSearchQuery(Query: "Factory"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(accessToken, logger.Messages);
        Assert.DoesNotContain("private-collection", logger.Messages);
        Assert.DoesNotContain("img.discogs.com", logger.Messages);
        Assert.DoesNotContain("seller data", logger.Messages);
        Assert.DoesNotContain("wantlist", logger.Messages);
        Assert.DoesNotContain("internal exception text", logger.Messages);
        Assert.Contains("Unavailable", logger.Messages);
    }

    private static DiscogsExternalMetadataProvider CreateProvider(
        RecordingHttpMessageHandler handler,
        ILogger<DiscogsExternalMetadataProvider>? logger = null,
        string accessToken = "test-token")
    {
        HttpClient httpClient = new(handler)
        {
            BaseAddress = new Uri("https://api.discogs.test")
        };

        return new DiscogsExternalMetadataProvider(
            httpClient,
            Options.Create(new DiscogsOptions
            {
                Enabled = true,
                AccessToken = accessToken,
                UserAgent = "DiscWeave.Tests/1.0",
                BaseUrl = "https://api.discogs.test",
                TimeoutSeconds = 10
            }),
            logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DiscogsExternalMetadataProvider>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class CapturingLogger : ILogger<DiscogsExternalMetadataProvider>
    {
        private readonly List<string> _messages = [];

        public string Messages => string.Join(Environment.NewLine, _messages);

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _messages.Add(formatter(state, exception));
            if (exception is not null)
            {
                _messages.Add(exception.Message);
            }
        }
    }
}
