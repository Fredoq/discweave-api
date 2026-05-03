namespace Cratebase.Api.Features.TrackRelations;

public sealed record TrackRelationResponse(Guid Id, Guid SourceTrackId, Guid TargetTrackId, string Type);
