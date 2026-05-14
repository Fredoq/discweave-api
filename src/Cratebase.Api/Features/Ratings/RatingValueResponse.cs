namespace Cratebase.Api.Features.Ratings;

public sealed record RatingValueResponse(
    Guid Id,
    Guid CriterionId,
    string TargetType,
    Guid TargetId,
    int Value);
