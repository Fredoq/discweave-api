using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;

namespace Cratebase.Domain.Relations;

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
        TrackRelationType relationType)
    {
        CollectionId = collectionId;
        Id = id;
        SourceTrackId = sourceTrackId;
        TargetTrackId = targetTrackId;
        RelationType = relationType;
    }

    public CollectionId CollectionId { get; private set; }

    public TrackRelationId Id { get; private set; }

    public TrackId SourceTrackId { get; private set; }

    public TrackId TargetTrackId { get; private set; }

    public TrackRelationType RelationType { get; private set; }

    public static TrackRelation Create(
        TrackRelationId id,
        CollectionId collectionId,
        TrackId sourceTrackId,
        TrackId targetTrackId,
        TrackRelationType type)
    {
        return sourceTrackId == targetTrackId
            ? throw new DomainException(SelfRelationCode, SelfRelationMessage)
            : new TrackRelation(collectionId, id, sourceTrackId, targetTrackId, type);
    }

    public void Update(TrackId sourceTrackId, TrackId targetTrackId, TrackRelationType type)
    {
        if (sourceTrackId == targetTrackId)
        {
            throw new DomainException(SelfRelationCode, SelfRelationMessage);
        }

        SourceTrackId = sourceTrackId;
        TargetTrackId = targetTrackId;
        RelationType = type;
    }
}
