using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;
using Cratebase.Domain.SharedKernel.Validation;

namespace Cratebase.Domain.Credits;

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

    private Credit(CreditId id, CreditContributor contributor, CreditTarget target, CreditRole role)
    {
        Id = id;
        SetContributor(contributor);
        Role = role;
        SetTarget(target);
    }

    public CreditId Id { get; private set; }

    public CreditContributor Contributor => CreditContributor.Create(_contributorArtistId, _contributorName);

    public CreditTarget Target => CreateTarget();

    public CreditRole Role { get; private set; }

    public static Credit Create(CreditId id, CreditContributor contributor, CreditTarget target, CreditRole role)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        ArgumentNullException.ThrowIfNull(target);

        CreditRole validatedRole = Guard.DefinedEnum(role, nameof(role), "credit.role_invalid");

        return new Credit(id, contributor, target, validatedRole);
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
