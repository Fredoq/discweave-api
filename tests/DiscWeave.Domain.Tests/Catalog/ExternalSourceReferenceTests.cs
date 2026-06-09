using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Tests.Catalog;

public sealed class ExternalSourceReferenceTests
{
    private static readonly DateTimeOffset AppliedAt = new(2026, 5, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "External source references normalize safe provider metadata")]
    public void External_source_references_normalize_safe_provider_metadata()
    {
        var source = ExternalSourceReference.Create(
            " discogs ",
            " release ",
            " 249504 ",
            " https://www.discogs.com/release/249504 ",
            AppliedAt);

        Assert.Equal("discogs", source.ProviderName);
        Assert.Equal("release", source.ResourceType);
        Assert.Equal("249504", source.ExternalId);
        Assert.Equal("https://www.discogs.com/release/249504", source.SourceUrl);
        Assert.Equal(AppliedAt, source.AppliedAt);
    }

    [Theory(DisplayName = "External source references require absolute HTTP source URLs")]
    [InlineData("not-a-url")]
    [InlineData("ftp://discogs.example/release/249504")]
    public void External_source_references_require_absolute_http_source_urls(string sourceUrl)
    {
        DomainException exception = Assert.Throws<DomainException>(() =>
            ExternalSourceReference.Create("discogs", "release", "249504", sourceUrl, AppliedAt));

        Assert.Equal("external_source.source_url_invalid", exception.Code);
    }

    [Fact(DisplayName = "Catalog records reject duplicate external source references")]
    public void Catalog_records_reject_duplicate_external_source_references()
    {
        var release = Release.Create(CollectionId.New(), ReleaseId.New(), "Blue Monday");

        DomainException exception = Assert.Throws<DomainException>(() =>
            release.ReplaceExternalSources(
            [
                Source("release", "249504"),
                Source("release", "249504")
            ]));

        Assert.Equal("external_source.duplicate", exception.Code);
    }

    [Fact(DisplayName = "Artists releases and tracks can replace and clear external sources")]
    public void Artists_releases_and_tracks_can_replace_and_clear_external_sources()
    {
        var collectionId = CollectionId.New();
        var artist = Group.Create(collectionId, ArtistId.New(), "New Order");
        var release = Release.Create(collectionId, ReleaseId.New(), "Blue Monday");
        var track = Track.Create(collectionId, TrackId.New(), "Blue Monday");

        artist.ReplaceExternalSources([Source("artist", "5876")]);
        release.ReplaceExternalSources([Source("release", "249504")]);
        track.ReplaceExternalSources([Source("track", "249504-A")]);

        Assert.Equal("5876", Assert.Single(artist.ExternalSources).ExternalId);
        Assert.Equal("249504", Assert.Single(release.ExternalSources).ExternalId);
        Assert.Equal("249504-A", Assert.Single(track.ExternalSources).ExternalId);

        artist.ReplaceExternalSources([]);
        release.ReplaceExternalSources([]);
        track.ReplaceExternalSources([]);

        Assert.Empty(artist.ExternalSources);
        Assert.Empty(release.ExternalSources);
        Assert.Empty(track.ExternalSources);
    }

    private static ExternalSourceReference Source(string resourceType, string externalId)
    {
        return ExternalSourceReference.Create(
            "discogs",
            resourceType,
            externalId,
            $"https://www.discogs.com/{resourceType}/{externalId}",
            AppliedAt);
    }
}
