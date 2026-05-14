namespace Cratebase.Api.Features.Ratings;

public sealed record RatingShowcaseItemResponse(
    Guid CriterionId,
    string TargetType,
    Guid TargetId,
    string Title,
    string? Subtitle,
    int? Value);
