using System.Net;
using DiscWeave.Application.ExternalMetadata;
using DiscWeave.Infrastructure.ExternalMetadata.Discogs;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Tests;

public sealed class DiscogsArtistMappingTests
{
    [Fact(DisplayName = "Artist search sends Discogs query parameters and maps candidates")]
    public async Task Artist_search_sends_Discogs_query_parameters_and_maps_candidates()
    {
        RecordingHttpMessageHandler handler = JsonHandler(
            // lang=json
            """
            {
              "pagination": { "items": 1 },
              "results": [
                {
                  "type": "artist",
                  "id": 5876,
                  "title": "Arthur Baker",
                  "uri": "/artist/5876-Arthur-Baker"
                }
              ]
            }
            """);
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataSearchResult<ExternalMetadataArtistCandidate>> result =
            await provider.SearchArtistsAsync(new ExternalMetadataArtistSearchQuery("Arthur Baker", 10), CancellationToken.None);

        Assert.True(result.IsSuccess);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal("/database/search", request.RequestUri?.AbsolutePath);
        Assert.Contains("type=artist", request.RequestUri?.Query);
        Assert.Contains("q=Arthur%20Baker", request.RequestUri?.Query);
        Assert.Contains("per_page=10", request.RequestUri?.Query);
        ExternalMetadataArtistCandidate candidate = Assert.Single(result.Value.Items);
        Assert.Equal("discogs", candidate.Source.ProviderName);
        Assert.Equal("artist", candidate.Source.ResourceType);
        Assert.Equal("5876", candidate.Source.ExternalId);
        Assert.Equal("https://www.discogs.com/artist/5876-Arthur-Baker", candidate.Source.SourceUrl);
        Assert.Equal("Arthur Baker", candidate.Name);
    }

    private static DiscogsExternalMetadataProvider CreateProvider(RecordingHttpMessageHandler handler)
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
