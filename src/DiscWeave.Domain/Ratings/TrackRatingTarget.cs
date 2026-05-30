using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Ratings;

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
