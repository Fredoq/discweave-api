using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;

namespace DiscWeave.Domain.Ratings;

public sealed class RatingValue : IEntity<RatingValueId>
{
    private const string ArtistTargetType = "artist";
    private const string LabelTargetType = "label";
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";

    private string _targetType = string.Empty;
    private ArtistId? _targetArtistId;
    private ReleaseId? _targetReleaseId;
    private TrackId? _targetTrackId;
    private LabelId? _targetLabelId;

    private RatingValue()
    {
        Rating = Rating.FromValue(1);
    }

    private RatingValue(
        CollectionId collectionId,
        RatingValueId id,
        RatingCriterionId criterionId,
        RatingTarget target,
        Rating rating)
    {
        CollectionId = collectionId;
        Id = id;
        CriterionId = criterionId;
        SetTarget(target);
        UpdateRating(rating);
    }

    public CollectionId CollectionId { get; private set; }

    public RatingValueId Id { get; private set; }

    public RatingCriterionId CriterionId { get; private set; }

    public RatingTarget Target => CreateTarget();

    public Rating Rating { get; private set; } = null!;

    public static RatingValue Create(
        CollectionId collectionId,
        RatingValueId id,
        RatingCriterionId criterionId,
        RatingTarget target,
        Rating rating)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rating);

        return new RatingValue(collectionId, id, criterionId, target, rating);
    }

    public void UpdateRating(Rating rating)
    {
        ArgumentNullException.ThrowIfNull(rating);

        Rating = rating;
    }

    private void SetTarget(RatingTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        switch (target)
        {
            case ArtistRatingTarget artistTarget:
                _targetType = ArtistTargetType;
                _targetArtistId = artistTarget.ArtistId;
                _targetReleaseId = null;
                _targetTrackId = null;
                _targetLabelId = null;
                break;
            case ReleaseRatingTarget releaseTarget:
                _targetType = ReleaseTargetType;
                _targetArtistId = null;
                _targetReleaseId = releaseTarget.ReleaseId;
                _targetTrackId = null;
                _targetLabelId = null;
                break;
            case TrackRatingTarget trackTarget:
                _targetType = TrackTargetType;
                _targetArtistId = null;
                _targetReleaseId = null;
                _targetTrackId = trackTarget.TrackId;
                _targetLabelId = null;
                break;
            case LabelRatingTarget labelTarget:
                _targetType = LabelTargetType;
                _targetArtistId = null;
                _targetReleaseId = null;
                _targetTrackId = null;
                _targetLabelId = labelTarget.LabelId;
                break;
            default:
                throw new InvalidOperationException("Rating target type is not supported");
        }
    }

    private RatingTarget CreateTarget()
    {
        return _targetType switch
        {
            ArtistTargetType when _targetArtistId is { } artistId => RatingTarget.ForArtist(artistId),
            ReleaseTargetType when _targetReleaseId is { } releaseId => RatingTarget.ForRelease(releaseId),
            TrackTargetType when _targetTrackId is { } trackId => RatingTarget.ForTrack(trackId),
            LabelTargetType when _targetLabelId is { } labelId => RatingTarget.ForLabel(labelId),
            _ => throw new InvalidOperationException("Rating target payload is not valid")
        };
    }
}
