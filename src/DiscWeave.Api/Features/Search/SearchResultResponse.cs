namespace DiscWeave.Api.Features.Search;

public sealed record SearchResultResponse(
    Guid Id,
    string Type,
    string Title,
    string? Subtitle,
    string? Summary,
    IReadOnlyList<string> MatchedFields,
    IReadOnlyList<string> Snippets,
    SearchResultFacetsResponse Facets,
    decimal Rank);
