using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Credits;

public sealed class Credit : IEntity<CreditId>
{
    private const string ReleaseTargetType = "release";
    private const string TrackTargetType = "track";

    private string _targetType = string.Empty;
    private ReleaseId? _targetReleaseId;
    private TrackId? _targetTrackId;
    private ArtistId _contributorArtistId;

    private string _contributorName = string.Empty;

    private Credit()
    {
    }

    private Credit(CollectionId collectionId, CreditId id, CreditContributor contributor, CreditTarget target, string role)
    {
        CollectionId = collectionId;
        Id = id;
        SetContributor(contributor);
        Role = Guard.RequiredText(role, nameof(role), "credit.role_required");
        SetTarget(target);
    }

    public CollectionId CollectionId { get; private set; }

    public CreditId Id { get; private set; }

    public CreditContributor Contributor => CreditContributor.Create(_contributorArtistId, _contributorName);

    public CreditTarget Target => CreateTarget();

    public string Role { get; private set; } = string.Empty;

    public static string ToRoleCode(CreditRole role)
    {
        return Guard.DefinedEnum(role, nameof(role), "credit.role_invalid") switch
        {
            CreditRole.MainArtist => "mainArtist",
            CreditRole.FeaturedArtist => "featuredArtist",
            CreditRole.Remixer => "remixer",
            CreditRole.Producer => "producer",
            CreditRole.Composer => "composer",
            CreditRole.Performer => "performer",
            CreditRole.Engineer => "engineer",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Credit role is not supported")
        };
    }

    public static Credit Create(CollectionId collectionId, CreditId id, CreditContributor contributor, CreditTarget target, string role)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        ArgumentNullException.ThrowIfNull(target);

        return new Credit(collectionId, id, contributor, target, role);
    }

    public static Credit Create(CollectionId collectionId, CreditId id, CreditContributor contributor, CreditTarget target, CreditRole role)
    {
        return Create(collectionId, id, contributor, target, ToRoleCode(role));
    }

    public void Update(CreditContributor contributor, CreditTarget target, string role)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        ArgumentNullException.ThrowIfNull(target);

        SetContributor(contributor);
        SetTarget(target);
        Role = Guard.RequiredText(role, nameof(role), "credit.role_required");
    }

    public void Update(CreditContributor contributor, CreditTarget target, CreditRole role)
    {
        Update(contributor, target, ToRoleCode(role));
    }

    private void SetTarget(CreditTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        switch (target)
        {
            case ReleaseCreditTarget releaseTarget:
                _targetType = ReleaseTargetType;
                _targetReleaseId = releaseTarget.ReleaseId;
                _targetTrackId = null;
                break;
            case TrackCreditTarget trackTarget:
                _targetType = TrackTargetType;
                _targetReleaseId = null;
                _targetTrackId = trackTarget.TrackId;
                break;
            default:
                throw new InvalidOperationException("Credit target type is not supported");
        }
    }

    private void SetContributor(CreditContributor contributor)
    {
        _contributorArtistId = contributor.ArtistId;
        _contributorName = contributor.Name;
    }

    private CreditTarget CreateTarget()
    {
        return _targetType switch
        {
            ReleaseTargetType when _targetReleaseId is { } releaseId => CreditTarget.ForRelease(releaseId),
            TrackTargetType when _targetTrackId is { } trackId => CreditTarget.ForTrack(trackId),
            _ => throw new InvalidOperationException("Credit target payload is not valid")
        };
    }
}
