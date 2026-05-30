namespace DiscWeave.Api.Features.Ratings;

public sealed record RatingCriterionRequest(
    string Code,
    string Name,
    IReadOnlyList<string>? TargetTypes,
    int? SortOrder,
    bool? IsActive);
