namespace Cratebase.Api.Features.TrackRelations;

public sealed class TrackRelationListRequest
{
    public Guid? SourceTrackId { get; init; }

    public Guid? TargetTrackId { get; init; }

    public string? Type { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
