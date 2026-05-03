namespace Cratebase.Api.Features.ArtistRelations;

public sealed record ArtistRelationRequest(Guid SourceArtistId, Guid TargetArtistId, string Type, int? StartYear, int? EndYear);
