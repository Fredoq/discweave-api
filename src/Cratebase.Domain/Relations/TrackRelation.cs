using Cratebase.Domain.SharedKernel.Errors;
using Cratebase.Domain.SharedKernel.Ids;
using Cratebase.Domain.SharedKernel.Interfaces;

namespace Cratebase.Domain.Relations;

public sealed class TrackRelation : IEntity<TrackRelationId>
{
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

    public CollectionId CollectionId { get; }

    public TrackRelationId Id { get; }

    public TrackId SourceTrackId { get; }

    public TrackId TargetTrackId { get; }

    public TrackRelationType RelationType { get; }

    public static TrackRelation Create(
        TrackRelationId id,
        CollectionId collectionId,
        TrackId sourceTrackId,
        TrackId targetTrackId,
        TrackRelationType type)
    {
        return sourceTrackId == targetTrackId
            ? throw new DomainException("track_relation.self_relation", "Track relation cannot reference the same track twice")
            : new TrackRelation(collectionId, id, sourceTrackId, targetTrackId, type);
    }
}
