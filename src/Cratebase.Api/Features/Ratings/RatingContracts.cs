namespace Cratebase.Api.Features.Ratings;

public sealed record RatingCriterionRequest(
    string Code,
    string Name,
    IReadOnlyList<string>? TargetTypes,
    int? SortOrder,
    bool? IsActive);

public sealed record UpdateRatingCriterionRequest(
    string Name,
    IReadOnlyList<string>? TargetTypes,
    int? SortOrder,
    bool? IsActive);

public sealed record RatingCriterionResponse(
    Guid Id,
    string Code,
    string Name,
    IReadOnlyList<string> TargetTypes,
    int SortOrder,
    bool IsActive,
    bool IsBuiltin,
    bool IsProtected);

public sealed record RatingValueRequest(int Value);

public sealed record RatingValueResponse(
    Guid Id,
    Guid CriterionId,
    string TargetType,
    Guid TargetId,
    int Value);

public sealed record RatingShowcaseItemResponse(
    Guid CriterionId,
    string TargetType,
    Guid TargetId,
    string Title,
    string? Subtitle,
    int? Value);

public sealed record RatingShowcaseResponse(
    IReadOnlyList<RatingShowcaseItemResponse> Items,
    int Limit,
    int Offset,
    int Total);
