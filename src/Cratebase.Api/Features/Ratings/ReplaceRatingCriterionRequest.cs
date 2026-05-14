namespace Cratebase.Api.Features.Ratings;

public sealed record ReplaceRatingCriterionRequest(
    string Name,
    IReadOnlyList<string>? TargetTypes,
    int SortOrder,
    bool IsActive);
