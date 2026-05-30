using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Optional;

namespace DiscWeave.Domain.Ratings;

public sealed class ReleaseTrackRatingSummary
{
    private ReleaseTrackRatingSummary(IOptionalValue<decimal> averageRating, int ratedTrackCount)
    {
        if (ratedTrackCount < 0)
        {
            throw new DomainException("release_track_rating_summary.count_negative", "Rated track count cannot be negative");
        }

        if (ratedTrackCount == 0 && averageRating.HasValue)
        {
            throw new DomainException("release_track_rating_summary.invalid_state", "Unrated summary cannot have an average rating");
        }

        if (ratedTrackCount > 0 && !averageRating.HasValue)
        {
            throw new DomainException("release_track_rating_summary.invalid_state", "Rated summary must have an average rating");
        }

        if (averageRating.Match(whenPresent: rating => rating is < 1m or > 10m, whenMissing: () => false))
        {
            throw new DomainException(
                "release_track_rating_summary.average_out_of_range",
                "Average rating must be between 1 and 10");
        }

        AverageRating = averageRating;
        RatedTrackCount = ratedTrackCount;
    }

    public IOptionalValue<decimal> AverageRating { get; }

    public int RatedTrackCount { get; }

    public static ReleaseTrackRatingSummary Unrated()
    {
        return new ReleaseTrackRatingSummary(Optional.Missing<decimal>(), 0);
    }

    public static ReleaseTrackRatingSummary FromAverage(decimal averageRating, int ratedTrackCount)
    {
        return new ReleaseTrackRatingSummary(Optional.From(averageRating), ratedTrackCount);
    }
}
