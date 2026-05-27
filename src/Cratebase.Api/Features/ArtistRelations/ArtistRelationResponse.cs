namespace Cratebase.Api.Features.ArtistRelations;

public sealed record ArtistRelationResponse(
    Guid Id,
    Guid SourceArtistId,
    Guid TargetArtistId,
    string Type,
    int? StartYear,
    int? EndYear,
    string? SourceArtistName = null,
    string? TargetArtistName = null);
