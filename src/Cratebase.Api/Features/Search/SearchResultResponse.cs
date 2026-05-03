namespace Cratebase.Api.Features.Search;

public sealed record SearchResultResponse(
    Guid Id,
    string Type,
    string Title,
    string? Subtitle,
    IReadOnlyList<string> MatchedFields);
