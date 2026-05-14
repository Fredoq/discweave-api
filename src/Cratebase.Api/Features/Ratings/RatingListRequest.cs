namespace Cratebase.Api.Features.Ratings;

public sealed class RatingListRequest
{
    public string? TargetType { get; init; }

    public Guid? TargetId { get; init; }

    public Guid? CriterionId { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
