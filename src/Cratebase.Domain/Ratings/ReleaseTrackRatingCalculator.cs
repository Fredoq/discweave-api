using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Ratings;

public static class ReleaseTrackRatingCalculator
{
    public static ReleaseTrackRatingSummary Calculate(
        Release release,
        IReadOnlyCollection<RatingValue> ratings,
        RatingCriterionId criterionId)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(ratings);

        var ratingsByTrackId = ratings
            .Where(rating => rating.CriterionId == criterionId && rating.Target is TrackRatingTarget)
            .GroupBy(rating => ((TrackRatingTarget)rating.Target).TrackId)
            .ToDictionary(group => group.Key, group => group.First());
        List<int> values = [];

        foreach (ReleaseTrack releaseTrack in release.Tracklist)
        {
            if (!ratingsByTrackId.TryGetValue(releaseTrack.TrackId, out RatingValue? rating))
            {
                continue;
            }

            values.Add(rating.Rating.Value);
        }

        return values.Count == 0
            ? ReleaseTrackRatingSummary.Unrated()
            : ReleaseTrackRatingSummary.FromAverage(decimal.Divide(values.Sum(), values.Count), values.Count);
    }
}
