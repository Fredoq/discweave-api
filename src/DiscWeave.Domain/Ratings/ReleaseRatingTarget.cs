using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Ratings;

public sealed class ReleaseRatingTarget : RatingTarget
{
    private ReleaseRatingTarget(ReleaseId releaseId)
    {
        ReleaseId = releaseId;
    }

    public override RatingTargetType Type => RatingTargetType.Release;

    public ReleaseId ReleaseId { get; }

    public static ReleaseRatingTarget Create(ReleaseId releaseId)
    {
        return new ReleaseRatingTarget(releaseId);
    }
}
