using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Ratings;

public static class RatingCriterionDefaults
{
    public const string OverallCode = "overall";

    public static IReadOnlyList<RatingCriterion> CreateCriteria(CollectionId collectionId)
    {
        return
        [
            RatingCriterion.CreateProtected(
                RatingCriterionId.New(),
                collectionId,
                OverallCode,
                "Overall",
                [RatingTargetType.Release, RatingTargetType.Track],
                10)
        ];
    }
}
