using System.Net;
using DiscWeave.Application.ExternalMetadata;
using DiscWeave.Infrastructure.ExternalMetadata.Discogs;
using Microsoft.Extensions.Options;

namespace DiscWeave.Infrastructure.Tests;

public sealed class DiscogsReleaseDetailMappingTests
{
    [Fact(DisplayName = "Release detail maps Discogs labels identifiers formats tracklist and credits")]
    public async Task Release_detail_maps_Discogs_labels_identifiers_formats_tracklist_and_credits()
    {
        RecordingHttpMessageHandler handler = JsonHandler(
            // lang=json
            """
            {
              "id": 249504,
              "title": "Blue Monday",
              "year": 1983,
              "released": "1983-03-07",
              "uri": "/release/249504-New-Order-Blue-Monday",
              "artists": [ { "name": "New Order" } ],
              "genres": [ "Electronic" ],
              "styles": [ "Synth-pop", "Leftfield" ],
              "labels": [ { "name": "Factory", "catno": "FAC 73" } ],
              "formats": [ { "name": "Vinyl", "descriptions": [ "12\"", "Single" ] } ],
              "identifiers": [
                { "type": "Barcode", "value": "5016839200371" },
                { "type": "Matrix / Runout", "value": "FAC 73 A" }
              ],
              "extraartists": [ { "name": "Producer Name", "role": "Producer" } ],
              "tracklist": [
                {
                  "type_": "heading",
                  "position": "",
                  "title": "Orbit Compact Disc",
                  "duration": "53:14",
                  "extraartists": [ { "name": "Heading Credit", "role": "Design" } ]
                },
                {
                  "type_": "track",
                  "position": "A",
                  "title": "Blue Monday",
                  "duration": "7:29",
                  "artists": [ { "name": "New Order" } ],
                  "extraartists": [ { "name": "Remixer Name", "role": "Remix" } ]
                }
              ]
            }
            """);
        DiscogsExternalMetadataProvider provider = CreateProvider(handler);

        ExternalMetadataResult<ExternalMetadataReleaseDetail> result =
            await provider.GetReleaseAsync(new ExternalMetadataLookupQuery("249504"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal("/releases/249504", request.RequestUri?.AbsolutePath);
        Assert.Equal("Blue Monday", result.Value.Title);
        Assert.Equal(1983, result.Value.Year);
        Assert.Equal(new DateOnly(1983, 3, 7), result.Value.ReleaseDate);
        Assert.Contains("New Order", result.Value.Artists);
        Assert.Equal(["Electronic", "Synth-pop", "Leftfield"], result.Value.Genres);
        Assert.Contains("Factory", result.Value.Labels);
        Assert.Contains("Vinyl", result.Value.Formats);
        Assert.Equal("single", result.Value.Type);
        Assert.Equal("FAC 73", result.Value.CatalogNumber);
        ExternalMetadataReleaseLabel label = Assert.Single(result.Value.LabelDetails);
        Assert.Equal("Factory", label.Name);
        Assert.Equal("FAC 73", label.CatalogNumber);
        Assert.Contains(result.Value.Identifiers, identifier => identifier.Type == "Barcode" && identifier.Value == "5016839200371");
        ExternalMetadataReleaseTrack track = Assert.Single(result.Value.Tracklist);
        Assert.Equal("A", track.Position);
        Assert.Equal(TimeSpan.FromSeconds(449), track.Duration);
        Assert.Contains("New Order", track.Artists);
        Assert.Contains(result.Value.Credits, credit => credit.Name == "Producer Name" && credit.Role == "Producer" && credit.TrackTitle is null);
        Assert.Contains(result.Value.Credits, credit => credit.Name == "Remixer Name" && credit.Role == "Remix" && credit.TrackTitle == "Blue Monday");
        Assert.DoesNotContain(result.Value.Credits, credit => credit.Name == "Heading Credit");
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
