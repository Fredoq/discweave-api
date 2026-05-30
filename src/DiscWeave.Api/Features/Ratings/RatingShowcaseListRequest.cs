namespace DiscWeave.Api.Features.Ratings;

public sealed class RatingShowcaseListRequest
{
    public Guid CriterionId { get; init; }

    public string TargetType { get; init; } = string.Empty;

    public string? Mode { get; init; }

    public string? Scope { get; init; }

    public int? Limit { get; init; }

    public int? Offset { get; init; }
}
