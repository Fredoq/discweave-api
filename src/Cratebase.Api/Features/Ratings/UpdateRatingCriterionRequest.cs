namespace Cratebase.Api.Features.Ratings;

public sealed record UpdateRatingCriterionRequest(
    string? Name,
    IReadOnlyList<string>? TargetTypes,
    int? SortOrder,
    bool? IsActive);
