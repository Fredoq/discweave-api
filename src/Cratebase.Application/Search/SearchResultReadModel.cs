namespace Cratebase.Application.Search;

public sealed record SearchResultReadModel(
    Guid Id,
    string Type,
    string Title,
    string? Subtitle,
    string? Summary,
    IReadOnlyList<string> MatchedFields,
    IReadOnlyList<string> Snippets,
    SearchResultFacetsReadModel Facets,
    decimal Rank);
