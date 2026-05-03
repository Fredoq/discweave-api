using Cratebase.Domain.Relations;
using Cratebase.Domain.SharedKernel.Errors;

namespace Cratebase.Api.Features.TrackRelations;

internal static class TrackRelationMapper
{
    public static TrackRelationType ParseType(string type)
    {
        return type.Trim() switch
        {
            "remixOf" => TrackRelationType.RemixOf,
            "versionOf" => TrackRelationType.VersionOf,
            "editOf" => TrackRelationType.EditOf,
            _ => throw new DomainException("track_relation.type_invalid", "Track relation type is invalid")
        };
    }

    public static TrackRelationResponse ToResponse(TrackRelation relation)
    {
        return new TrackRelationResponse(
            relation.Id.Value,
            relation.SourceTrackId.Value,
            relation.TargetTrackId.Value,
            ToTypeCode(relation.RelationType));
    }

    private static string ToTypeCode(TrackRelationType type)
    {
        return type switch
        {
            TrackRelationType.RemixOf => "remixOf",
            TrackRelationType.VersionOf => "versionOf",
            TrackRelationType.EditOf => "editOf",
            _ => throw new InvalidOperationException("Track relation type is not supported")
        };
    }
}
