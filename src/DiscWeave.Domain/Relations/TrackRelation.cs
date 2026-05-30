using DiscWeave.Domain.SharedKernel.Errors;
using DiscWeave.Domain.SharedKernel.Ids;
using DiscWeave.Domain.SharedKernel.Interfaces;
using DiscWeave.Domain.SharedKernel.Validation;

namespace DiscWeave.Domain.Relations;

public sealed class TrackRelation : IEntity<TrackRelationId>
{
    private const string SelfRelationCode = "track_relation.self_relation";
    private const string SelfRelationMessage = "Track relation cannot reference the same track twice";

    private TrackRelation()
    {
    }

    private TrackRelation(
        CollectionId collectionId,
        TrackRelationId id,
        TrackId sourceTrackId,
        TrackId targetTrackId,
        string relationType)
    {
        CollectionId = collectionId;
        Id = id;
        SourceTrackId = sourceTrackId;
        TargetTrackId = targetTrackId;
        RelationType = Guard.RequiredText(relationType, nameof(relationType), "track_relation.type_required");
    }

    public CollectionId CollectionId { get; private set; }

    public TrackRelationId Id { get; private set; }

    public TrackId SourceTrackId { get; private set; }

    public TrackId TargetTrackId { get; private set; }

    public string RelationType { get; private set; } = string.Empty;

    public static TrackRelation Create(
        TrackRelationId id,
        CollectionId collectionId,
        TrackId sourceTrackId,
        TrackId targetTrackId,
        string type)
    {
        return sourceTrackId == targetTrackId
            ? throw new DomainException(SelfRelationCode, SelfRelationMessage)
            : new TrackRelation(collectionId, id, sourceTrackId, targetTrackId, type);
    }

    public static TrackRelation Create(
        TrackRelationId id,
        CollectionId collectionId,
        TrackId sourceTrackId,
        TrackId targetTrackId,
        TrackRelationType type)
    {
        return Create(id, collectionId, sourceTrackId, targetTrackId, ToTypeCode(type));
    }

    public void Update(TrackId sourceTrackId, TrackId targetTrackId, string type)
    {
        if (sourceTrackId == targetTrackId)
        {
            throw new DomainException(SelfRelationCode, SelfRelationMessage);
        }

        SourceTrackId = sourceTrackId;
        TargetTrackId = targetTrackId;
        RelationType = Guard.RequiredText(type, nameof(type), "track_relation.type_required");
    }

    public void Update(TrackId sourceTrackId, TrackId targetTrackId, TrackRelationType type)
    {
        Update(sourceTrackId, targetTrackId, ToTypeCode(type));
    }

    private static string ToTypeCode(TrackRelationType type)
    {
        return Guard.DefinedEnum(type, nameof(type), "track_relation.type_invalid") switch
        {
            TrackRelationType.RemixOf => "remixOf",
            TrackRelationType.VersionOf => "versionOf",
            TrackRelationType.EditOf => "editOf",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Track relation type is not supported")
        };
    }
}
