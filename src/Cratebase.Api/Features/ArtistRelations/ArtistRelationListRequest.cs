namespace Cratebase.Api.Features.ArtistRelations;

public sealed class ArtistRelationListRequest
{
    public Guid? SourceArtistId { get; init; }

    public Guid? TargetArtistId { get; init; }

    public string? Type { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
