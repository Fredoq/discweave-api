using DiscWeave.Domain.Relations;
using DiscWeave.Domain.SharedKernel.Errors;

namespace DiscWeave.Api.Features.TrackRelations;

internal static class TrackRelationMapper
{
    public static string ParseType(string type)
    {
        return string.IsNullOrWhiteSpace(type)
            ? throw new DomainException("track_relation.type_invalid", "Track relation type is invalid")
            : type.Trim();
    }

    public static TrackRelationResponse ToResponse(TrackRelation relation, string? sourceTrackTitle = null, string? targetTrackTitle = null)
    {
        return new TrackRelationResponse(
            relation.Id.Value,
            relation.SourceTrackId.Value,
            relation.TargetTrackId.Value,
            relation.RelationType,
            sourceTrackTitle,
            targetTrackTitle);
    }
}
