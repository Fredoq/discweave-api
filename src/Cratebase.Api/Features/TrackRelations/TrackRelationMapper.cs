using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;

namespace Cratebase.Api.Features.TrackRelations;

internal static class TrackRelationMapper
{
    public static string ParseType(string type)
    {
        return string.IsNullOrWhiteSpace(type)
            ? throw new DomainException("track_relation.type_invalid", "Track relation type is invalid")
            : type.Trim();
    }

    public static TrackRelationResponse ToResponse(TrackRelation relation)
    {
        return new TrackRelationResponse(
            relation.Id.Value,
            relation.SourceTrackId.Value,
            relation.TargetTrackId.Value,
            relation.RelationType);
    }
}
