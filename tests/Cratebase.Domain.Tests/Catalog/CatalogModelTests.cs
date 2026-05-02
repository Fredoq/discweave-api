using Cratebase.Domain.Catalog;
using Cratebase.Domain.Ratings;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Tests.Catalog;

public sealed class CatalogModelTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Named_catalog_models_reject_blank_names_and_titles(string value)
    {
        Assert.Equal("artist.name_required", Assert.Throws<DomainException>(() => Person.Create(CollectionId.New(), ArtistId.New(), value)).Code);
        Assert.Equal("artist.name_required", Assert.Throws<DomainException>(() => Group.Create(CollectionId.New(), ArtistId.New(), value)).Code);
        Assert.Equal("label.name_required", Assert.Throws<DomainException>(() => Label.Create(CollectionId.New(), LabelId.New(), value)).Code);
        Assert.Equal("track.title_required", Assert.Throws<DomainException>(() => Track.Create(CollectionId.New(), TrackId.New(), value)).Code);
        Assert.Equal("release.title_required", Assert.Throws<DomainException>(() => Release.Create(CollectionId.New(), ReleaseId.New(), value)).Code);
    }

    [Fact]
    public void Artists_can_be_renamed_without_changing_identity_or_type()
    {
        var artistId = ArtistId.New();
        Artist person = Person.Create(CollectionId.New(), artistId, "  Bernard Sumner  ");
        Artist group = Group.Create(CollectionId.New(), ArtistId.New(), "New Order");

        person.Rename("  Bernard Dicken  ");
        group.Rename("  Joy Division  ");

        Assert.Equal(artistId, person.Id);
        _ = Assert.IsType<Person>(person);
        Assert.Equal("Bernard Dicken", person.Name);
        Assert.Equal("Joy Division", group.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Artist_rename_rejects_blank_names(string value)
    {
        Artist artist = Person.Create(CollectionId.New(), ArtistId.New(), "Bernard Sumner");

        DomainException exception = Assert.Throws<DomainException>(() => artist.Rename(value));

        Assert.Equal("artist.name_required", exception.Code);
        Assert.Equal("Bernard Sumner", artist.Name);
    }

    [Fact]
    public void Labels_can_be_renamed_without_changing_identity()
    {
        var labelId = LabelId.New();
        var label = Label.Create(CollectionId.New(), labelId, "  Factory  ");

        label.Rename("  Factory Records  ");

        Assert.Equal(labelId, label.Id);
        Assert.Equal("Factory Records", label.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Label_rename_rejects_blank_names(string value)
    {
        var label = Label.Create(CollectionId.New(), LabelId.New(), "Factory");

        DomainException exception = Assert.Throws<DomainException>(() => label.Rename(value));

        Assert.Equal("label.name_required", exception.Code);
        Assert.Equal("Factory", label.Name);
    }

    [Fact]
    public void The_same_track_can_appear_on_multiple_releases_and_keep_one_canonical_rating()
    {
        Track track = Track.Create(CollectionId.New(), TrackId.New(), "Blue Monday").WithRating(Rating.FromValue(10));
        var firstReleaseId = ReleaseId.New();
        var secondReleaseId = ReleaseId.New();
        Release firstRelease = Release.Create(CollectionId.New(), firstReleaseId, "Blue Monday")
            .WithTrack(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(1)));
        Release secondRelease = Release.Create(CollectionId.New(), secondReleaseId, "Substance")
            .WithTrack(ReleaseTrack.Create(track.Id, TrackPosition.FromNumber(5)));

        Assert.Equal(track.Id, firstRelease.Tracklist.Single().TrackId);
        Assert.Equal(track.Id, secondRelease.Tracklist.Single().TrackId);
        Assert.Equal(10, Assert.IsType<PresentOptionalValue<Rating>>(track.Details.Rating).Value.Value);
    }

    [Fact]
    public void Track_position_rejects_non_positive_numbers_and_normalizes_markers()
    {
        DomainException exception = Assert.Throws<DomainException>(() => TrackPosition.FromNumber(0));
        _ = Assert.Throws<ArgumentNullException>(() => TrackPosition.FromNumber(1, null!, "A"));
        _ = Assert.Throws<ArgumentNullException>(() => TrackPosition.FromNumber(1, "1", null!));
        var position = TrackPosition.FromNumber(1, " A ", "  ");

        Assert.Equal("track_position.number_required", exception.Code);
        Assert.Equal("A", Assert.IsType<PresentOptionalValue<string>>(position.Disc).Value);
        Assert.False(position.Side.HasValue);
    }

    [Fact]
    public void Release_track_normalizes_blank_title_override()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            ReleaseTrack.Create(
                TrackId.New(),
                TrackPosition.FromNumber(1),
                null!));

        var releaseTrack = ReleaseTrack.Create(
            TrackId.New(),
            TrackPosition.FromNumber(1),
            "   ");

        Assert.False(releaseTrack.TitleOverride.HasValue);
    }

    [Fact]
    public void Release_rating_is_independent_from_average_track_rating()
    {
        Track firstTrack = Track.Create(CollectionId.New(), TrackId.New(), "Age of Consent").WithRating(Rating.FromValue(10));
        Track secondTrack = Track.Create(CollectionId.New(), TrackId.New(), "We All Stand").WithRating(Rating.FromValue(8));
        var releaseId = ReleaseId.New();
        Release release = Release.Create(CollectionId.New(), releaseId, "Power, Corruption & Lies")
            .WithRating(Rating.FromValue(7))
            .WithTrack(ReleaseTrack.Create(firstTrack.Id, TrackPosition.FromNumber(1)))
            .WithTrack(ReleaseTrack.Create(secondTrack.Id, TrackPosition.FromNumber(2)));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [firstTrack, secondTrack]);

        Assert.Equal(7, Assert.IsType<PresentOptionalValue<Rating>>(release.Summary.Rating).Value.Value);
        Assert.Equal(9m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(2, summary.RatedTrackCount);
    }

    [Fact]
    public void Release_track_rating_summary_ignores_unrated_tracks_and_can_be_empty()
    {
        Track ratedTrack = Track.Create(CollectionId.New(), TrackId.New(), "Leave Me Alone").WithRating(Rating.FromValue(9));
        var unratedTrack = Track.Create(CollectionId.New(), TrackId.New(), "The Village");
        var releaseId = ReleaseId.New();
        Release release = Release.Create(CollectionId.New(), releaseId, "Power, Corruption & Lies")
            .WithTrack(ReleaseTrack.Create(ratedTrack.Id, TrackPosition.FromNumber(8)))
            .WithTrack(ReleaseTrack.Create(unratedTrack.Id, TrackPosition.FromNumber(9)));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [ratedTrack, unratedTrack]);
        ReleaseTrackRatingSummary emptySummary = ReleaseTrackRatingCalculator.Calculate(release, [unratedTrack]);

        Assert.Equal(9m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(1, summary.RatedTrackCount);
        Assert.False(emptySummary.AverageRating.HasValue);
        Assert.Equal(0, emptySummary.RatedTrackCount);
    }

    [Fact]
    public void Release_track_rating_summary_tolerates_duplicate_track_snapshots()
    {
        Track ratedTrack = Track.Create(CollectionId.New(), TrackId.New(), "Ceremony").WithRating(Rating.FromValue(10));
        Track duplicateSnapshot = Track.Create(CollectionId.New(), ratedTrack.Id, "Ceremony").WithRating(Rating.FromValue(8));
        var releaseId = ReleaseId.New();
        Release release = Release.Create(CollectionId.New(), releaseId, "Ceremony")
            .WithTrack(ReleaseTrack.Create(ratedTrack.Id, TrackPosition.FromNumber(1)));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [ratedTrack, duplicateSnapshot]);

        Assert.Equal(10m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(1, summary.RatedTrackCount);
    }

    [Fact]
    public void Release_rejects_duplicate_track_positions()
    {
        var releaseId = ReleaseId.New();
        Release release = Release.Create(CollectionId.New(), releaseId, "Low-Life")
            .WithTrack(ReleaseTrack.Create(TrackId.New(), TrackPosition.FromNumber(1)));
        var duplicatePosition = ReleaseTrack.Create(TrackId.New(), TrackPosition.FromNumber(1));

        DomainException exception = Assert.Throws<DomainException>(() => release.WithTrack(duplicatePosition));

        Assert.Equal("release_track.position_duplicate", exception.Code);
    }

    [Fact]
    public void Release_can_store_type_and_cover_image()
    {
        var labelId = LabelId.New();
        var releaseDate = new DateOnly(1989, 1, 30);
        ReleaseMetadata metadata = ReleaseMetadata.Empty
            .WithType(ReleaseType.Album)
            .WithLabel(labelId)
            .WithReleaseYear(1989)
            .WithReleaseDate(releaseDate)
            .WithCoverImage(CoverImage.FromPath("covers/new-order-technique.jpg"));
        Cataloging cataloging = Cataloging.Empty
            .WithGenre(Genre.FromName("Alternative Dance"))
            .WithTag(Tag.FromName("favorite"));
        Release release = Release.Create(CollectionId.New(), ReleaseId.New(), "Technique")
            .WithSummary(ReleaseSummary.Create("Technique").WithMetadata(metadata))
            .WithCataloging(cataloging);

        ReleaseMetadata actualMetadata = release.Summary.Metadata;

        Assert.Equal(ReleaseType.Album, actualMetadata.Type);
        Assert.Equal(labelId, Assert.IsType<PresentOptionalValue<LabelId>>(actualMetadata.LabelId).Value);
        Assert.Equal(1989, Assert.IsType<PresentOptionalValue<int>>(actualMetadata.Year).Value);
        Assert.Equal(releaseDate, Assert.IsType<PresentOptionalValue<DateOnly>>(actualMetadata.ReleaseDate).Value);
        Assert.Equal(
            "covers/new-order-technique.jpg",
            Assert.IsType<PresentOptionalValue<CoverImage>>(actualMetadata.CoverImage).Value.Path);
        Assert.Contains(release.Cataloging.Genres, genre => genre.Name == "Alternative Dance");
        Assert.Contains(release.Cataloging.Tags, tag => tag.Name == "favorite");
    }

    [Fact]
    public void Release_can_update_summary_and_cataloging_without_changing_identity()
    {
        var releaseId = ReleaseId.New();
        Release release = Release.Create(CollectionId.New(), releaseId, "Technique")
            .WithCataloging(Cataloging.Empty.WithGenre(Genre.FromName("Alternative Dance")));
        ReleaseSummary updatedSummary = ReleaseSummary.Create("Technique 2020")
            .WithMetadata(ReleaseMetadata.Empty.WithType(ReleaseType.Album).WithReleaseYear(2020));
        Cataloging updatedCataloging = Cataloging.Empty
            .WithGenre(Genre.FromName("Dance-rock"))
            .WithTag(Tag.FromName("remaster"));

        release.UpdateSummary(updatedSummary);
        release.UpdateCataloging(updatedCataloging);

        Assert.Equal(releaseId, release.Id);
        Assert.Equal("Technique 2020", release.Summary.Title);
        Assert.Equal(ReleaseType.Album, release.Summary.Metadata.Type);
        Assert.Equal(2020, Assert.IsType<PresentOptionalValue<int>>(release.Summary.Metadata.Year).Value);
        Assert.Contains(release.Cataloging.Genres, genre => genre.Name == "Dance-rock");
        Assert.Contains(release.Cataloging.Tags, tag => tag.Name == "remaster");
        Assert.DoesNotContain(release.Cataloging.Genres, genre => genre.Name == "Alternative Dance");
    }

    [Fact]
    public void Release_type_is_a_closed_object_catalog()
    {
        Assert.Equal(ReleaseType.Album, ReleaseType.Album);
        Assert.NotEqual(ReleaseType.Album, ReleaseType.Ep);
    }

    [Fact]
    public void Cover_image_validates_required_values()
    {
        Assert.Equal("cover_image.path_required", Assert.Throws<DomainException>(() => CoverImage.FromPath(" ")).Code);
    }

    [Fact]
    public void Track_duration_must_be_positive_when_present()
    {
        var track = Track.Create(CollectionId.New(), TrackId.New(), "Dreams Never End");

        DomainException exception = Assert.Throws<DomainException>(() => track.WithDuration(TimeSpan.Zero));

        Assert.Equal("track.duration_required", exception.Code);
    }

    [Fact]
    public void Track_can_store_duration_genres_and_tags()
    {
        Track track = Track.Create(CollectionId.New(), TrackId.New(), "Dreams Never End")
            .WithDuration(TimeSpan.FromMinutes(3))
            .WithCataloging(
                Cataloging.Empty
                    .WithGenre(Genre.FromName("Post-punk"))
                    .WithTag(Tag.FromName("opener")));

        Assert.Equal(TimeSpan.FromMinutes(3), Assert.IsType<PresentOptionalValue<TimeSpan>>(track.Details.Duration).Value);
        Assert.Contains(track.Cataloging.Genres, genre => genre.Name == "Post-punk");
        Assert.Contains(track.Cataloging.Tags, tag => tag.Name == "opener");
    }
}
