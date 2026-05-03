namespace Cratebase.Api.Features.TrackRelations;

public sealed record TrackRelationRequest(Guid SourceTrackId, Guid TargetTrackId, string Type);
