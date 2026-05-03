using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Features.Credits;

internal static class CreditMapper
{
    public static CreditRole ParseRole(string role)
    {
        return role.Trim() switch
        {
            "mainArtist" => CreditRole.MainArtist,
            "featuredArtist" => CreditRole.FeaturedArtist,
            "remixer" => CreditRole.Remixer,
            "producer" => CreditRole.Producer,
            "composer" => CreditRole.Composer,
            "performer" => CreditRole.Performer,
            "engineer" => CreditRole.Engineer,
            _ => throw new DomainException("credit.role_invalid", "Credit role is invalid")
        };
    }

    public static CreditTarget CreateTarget(string targetType, Guid targetId)
    {
        return targetType.Trim() switch
        {
            "release" => CreditTarget.ForRelease(new ReleaseId(targetId)),
            "track" => CreditTarget.ForTrack(new TrackId(targetId)),
            _ => throw new DomainException("credit.target_type_invalid", "Credit target type is invalid")
        };
    }

    public static CreditResponse ToResponse(Credit credit)
    {
        CreditTarget target = credit.Target;
        (string targetType, Guid targetId) = target switch
        {
            ReleaseCreditTarget releaseTarget => ("release", releaseTarget.ReleaseId.Value),
            TrackCreditTarget trackTarget => ("track", trackTarget.TrackId.Value),
            _ => throw new InvalidOperationException("Credit target type is not supported")
        };

        return new CreditResponse(
            credit.Id.Value,
            credit.Contributor.ArtistId.Value,
            credit.Contributor.Name,
            targetType,
            targetId,
            ToRoleCode(credit.Role));
    }

    public static string ToRoleCode(CreditRole role)
    {
        return role switch
        {
            CreditRole.MainArtist => "mainArtist",
            CreditRole.FeaturedArtist => "featuredArtist",
            CreditRole.Remixer => "remixer",
            CreditRole.Producer => "producer",
            CreditRole.Composer => "composer",
            CreditRole.Performer => "performer",
            CreditRole.Engineer => "engineer",
            _ => throw new InvalidOperationException("Credit role is not supported")
        };
    }
}
