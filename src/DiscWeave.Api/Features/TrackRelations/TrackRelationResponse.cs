namespace DiscWeave.Api.Features.TrackRelations;

public sealed record TrackRelationResponse(
    Guid Id,
    Guid SourceTrackId,
    Guid TargetTrackId,
    string Type,
    string? SourceTrackTitle = null,
    string? TargetTrackTitle = null);
