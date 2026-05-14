using Cratebase.Domain.Ratings;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Optional;

namespace Cratebase.Domain.Tests.Ratings;

public sealed class RatingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    public void Rating_accepts_values_from_one_to_ten(int value)
    {
        var rating = Rating.FromValue(value);

        Assert.Equal(value, rating.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Rating_rejects_values_outside_one_to_ten(int value)
    {
        DomainException exception = Assert.Throws<DomainException>(() => Rating.FromValue(value));

        Assert.Equal("rating.out_of_range", exception.Code);
    }

    [Fact]
    public void Release_track_rating_summary_rejects_invalid_states()
    {
        DomainException negativeCount = Assert.Throws<DomainException>(() => ReleaseTrackRatingSummary.FromAverage(8m, -1));
        DomainException unratedWithAverage = Assert.Throws<DomainException>(() => ReleaseTrackRatingSummary.FromAverage(8m, 0));
        DomainException lowAverage = Assert.Throws<DomainException>(() => ReleaseTrackRatingSummary.FromAverage(0m, 1));
        DomainException highAverage = Assert.Throws<DomainException>(() => ReleaseTrackRatingSummary.FromAverage(11m, 1));

        Assert.Equal("release_track_rating_summary.count_negative", negativeCount.Code);
        Assert.Equal("release_track_rating_summary.invalid_state", unratedWithAverage.Code);
        Assert.Equal("release_track_rating_summary.average_out_of_range", lowAverage.Code);
        Assert.Equal("release_track_rating_summary.average_out_of_range", highAverage.Code);
    }

    [Fact]
    public void Rating_criterion_requires_at_least_one_unique_target_type()
    {
        var collectionId = CollectionId.New();

        DomainException emptyTargets = Assert.Throws<DomainException>(() =>
            RatingCriterion.Create(RatingCriterionId.New(), collectionId, "dancefloor", "Dancefloor energy", [], 20));
        DomainException duplicateTargets = Assert.Throws<DomainException>(() =>
            RatingCriterion.Create(
                RatingCriterionId.New(),
                collectionId,
                "dancefloor",
                "Dancefloor energy",
                [RatingTargetType.Track, RatingTargetType.Track],
                20));

        Assert.Equal("rating_criterion.target_required", emptyTargets.Code);
        Assert.Equal("rating_criterion.target_duplicate", duplicateTargets.Code);
    }

    [Fact]
    public void Protected_rating_criterion_cannot_be_deactivated_or_retargeted()
    {
        var criterion = RatingCriterion.CreateProtected(
            RatingCriterionId.New(),
            CollectionId.New(),
            "overall",
            "Overall",
            [RatingTargetType.Release, RatingTargetType.Track],
            10);

        DomainException deactivate = Assert.Throws<DomainException>(criterion.Deactivate);
        DomainException retarget = Assert.Throws<DomainException>(() =>
            criterion.Update("Overall", [RatingTargetType.Track], 10, isActive: true));

        Assert.True(criterion.IsActive);
        Assert.True(criterion.IsProtected);
        Assert.True(criterion.AppliesTo(RatingTargetType.Release));
        Assert.Equal("rating_criterion.protected", deactivate.Code);
        Assert.Equal("rating_criterion.protected", retarget.Code);
    }

    [Fact]
    public void Rating_value_keeps_a_single_typed_target_and_updates_rating()
    {
        var collectionId = CollectionId.New();
        var trackId = TrackId.New();
        var criterionId = RatingCriterionId.New();
        var rating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(trackId),
            Rating.FromValue(7));

        rating.UpdateRating(Rating.FromValue(9));

        Assert.Equal(collectionId, rating.CollectionId);
        Assert.Equal(criterionId, rating.CriterionId);
        Assert.Equal(9, rating.Rating.Value);
        Assert.Equal(trackId, Assert.IsType<TrackRatingTarget>(rating.Target).TrackId);
    }

    [Fact]
    public void Release_track_rating_summary_uses_track_ratings_for_the_requested_criterion()
    {
        var collectionId = CollectionId.New();
        var criterionId = RatingCriterionId.New();
        var firstTrack = Track.Create(collectionId, TrackId.New(), "Age of Consent");
        var secondTrack = Track.Create(collectionId, TrackId.New(), "We All Stand");
        Release release = Release.Create(collectionId, ReleaseId.New(), "Power, Corruption & Lies")
            .WithTrack(ReleaseTrack.Create(firstTrack.Id, TrackPosition.FromNumber(1)))
            .WithTrack(ReleaseTrack.Create(secondTrack.Id, TrackPosition.FromNumber(2)));
        var firstRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(firstTrack.Id),
            Rating.FromValue(10));
        var secondRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(secondTrack.Id),
            Rating.FromValue(8));
        var unrelatedRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            RatingCriterionId.New(),
            RatingTarget.ForTrack(firstTrack.Id),
            Rating.FromValue(1));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(
            release,
            [firstRating, secondRating, unrelatedRating],
            criterionId);

        Assert.Equal(9m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(2, summary.RatedTrackCount);
    }

    [Fact]
    public void Release_rating_is_independent_from_average_track_rating()
    {
        var collectionId = CollectionId.New();
        var criterionId = RatingCriterionId.New();
        var firstTrack = Track.Create(collectionId, TrackId.New(), "Age of Consent");
        var secondTrack = Track.Create(collectionId, TrackId.New(), "We All Stand");
        Release release = Release.Create(collectionId, ReleaseId.New(), "Power, Corruption & Lies")
            .WithTrack(ReleaseTrack.Create(firstTrack.Id, TrackPosition.FromNumber(1)))
            .WithTrack(ReleaseTrack.Create(secondTrack.Id, TrackPosition.FromNumber(2)));
        var releaseRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForRelease(release.Id),
            Rating.FromValue(7));
        var firstTrackRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(firstTrack.Id),
            Rating.FromValue(10));
        var secondTrackRating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(secondTrack.Id),
            Rating.FromValue(8));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [releaseRating, firstTrackRating, secondTrackRating], criterionId);

        Assert.Equal(7, releaseRating.Rating.Value);
        Assert.Equal(9m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(2, summary.RatedTrackCount);
    }

    [Fact]
    public void Release_track_rating_summary_ignores_unrated_tracks_and_can_be_empty()
    {
        var collectionId = CollectionId.New();
        var criterionId = RatingCriterionId.New();
        var ratedTrack = Track.Create(collectionId, TrackId.New(), "Leave Me Alone");
        var unratedTrack = Track.Create(collectionId, TrackId.New(), "The Village");
        Release release = Release.Create(collectionId, ReleaseId.New(), "Power, Corruption & Lies")
            .WithTrack(ReleaseTrack.Create(ratedTrack.Id, TrackPosition.FromNumber(8)))
            .WithTrack(ReleaseTrack.Create(unratedTrack.Id, TrackPosition.FromNumber(9)));
        var rating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(ratedTrack.Id),
            Rating.FromValue(9));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [rating], criterionId);
        ReleaseTrackRatingSummary emptySummary = ReleaseTrackRatingCalculator.Calculate(release, [], criterionId);

        Assert.Equal(9m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(1, summary.RatedTrackCount);
        Assert.False(emptySummary.AverageRating.HasValue);
        Assert.Equal(0, emptySummary.RatedTrackCount);
    }

    [Fact]
    public void Release_track_rating_summary_tolerates_duplicate_track_snapshots()
    {
        var collectionId = CollectionId.New();
        var criterionId = RatingCriterionId.New();
        var ratedTrack = Track.Create(collectionId, TrackId.New(), "Ceremony");
        Release release = Release.Create(collectionId, ReleaseId.New(), "Ceremony")
            .WithTrack(ReleaseTrack.Create(ratedTrack.Id, TrackPosition.FromNumber(1)));
        var rating = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(ratedTrack.Id),
            Rating.FromValue(10));
        var duplicateSnapshot = RatingValue.Create(
            collectionId,
            RatingValueId.New(),
            criterionId,
            RatingTarget.ForTrack(ratedTrack.Id),
            Rating.FromValue(8));

        ReleaseTrackRatingSummary summary = ReleaseTrackRatingCalculator.Calculate(release, [rating, duplicateSnapshot], criterionId);

        Assert.Equal(10m, Assert.IsType<PresentOptionalValue<decimal>>(summary.AverageRating).Value);
        Assert.Equal(1, summary.RatedTrackCount);
    }
}
