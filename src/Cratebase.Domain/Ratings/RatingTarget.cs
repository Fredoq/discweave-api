using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Ratings;

public abstract class RatingTarget
{
    private protected RatingTarget()
    {
    }

    public abstract RatingTargetType Type { get; }

    public static RatingTarget ForArtist(ArtistId artistId)
    {
        return ArtistRatingTarget.Create(artistId);
    }

    public static RatingTarget ForRelease(ReleaseId releaseId)
    {
        return ReleaseRatingTarget.Create(releaseId);
    }

    public static RatingTarget ForTrack(TrackId trackId)
    {
        return TrackRatingTarget.Create(trackId);
    }

    public static RatingTarget ForLabel(LabelId labelId)
    {
        return LabelRatingTarget.Create(labelId);
    }
}
