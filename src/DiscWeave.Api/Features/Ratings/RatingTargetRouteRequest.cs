namespace DiscWeave.Api.Features.Ratings;

public sealed class RatingTargetRouteRequest
{
    public string TargetType { get; init; } = string.Empty;

    public Guid TargetId { get; init; }

    public Guid CriterionId { get; init; }
}
