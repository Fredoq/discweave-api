namespace Cratebase.Api.Features.Ratings;

public sealed record RatingCriterionResponse(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> TargetTypes,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin,
    bool IsProtected);
