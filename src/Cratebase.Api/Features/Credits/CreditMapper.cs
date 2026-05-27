using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Api.Features.Credits;

internal static class CreditMapper
{
    public static string ParseRole(string role)
    {
        return string.IsNullOrWhiteSpace(role)
            ? throw new DomainException("credit.role_invalid", "Credit role is invalid")
            : role.Trim();
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

    public static CreditResponse ToResponse(Credit credit, string? targetTitle = null)
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
            ToRoleCode(credit.Role),
            targetTitle);
    }

    public static string ToRoleCode(string role)
    {
        return role;
    }
}
