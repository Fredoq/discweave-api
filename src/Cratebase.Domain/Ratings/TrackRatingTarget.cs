using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Ratings;

public sealed class TrackRatingTarget : RatingTarget
{
    private TrackRatingTarget(TrackId trackId)
    {
        TrackId = trackId;
    }

    public override RatingTargetType Type => RatingTargetType.Track;

    public TrackId TrackId { get; }

    public static TrackRatingTarget Create(TrackId trackId)
    {
        return new TrackRatingTarget(trackId);
    }
}
