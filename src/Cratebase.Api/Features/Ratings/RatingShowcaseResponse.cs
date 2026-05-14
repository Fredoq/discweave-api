namespace Cratebase.Api.Features.Ratings;

public sealed record RatingShowcaseResponse(
    IReadOnlyList<RatingShowcaseItemResponse> Items,
    int Limit,
    int Offset,
    int Total);
