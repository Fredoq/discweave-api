using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Domain.Ratings;

public sealed class ArtistRatingTarget : RatingTarget
{
    private ArtistRatingTarget(ArtistId artistId)
    {
        ArtistId = artistId;
    }

    public override RatingTargetType Type => RatingTargetType.Artist;

    public ArtistId ArtistId { get; }

    public static ArtistRatingTarget Create(ArtistId artistId)
    {
        return new ArtistRatingTarget(artistId);
    }
}
