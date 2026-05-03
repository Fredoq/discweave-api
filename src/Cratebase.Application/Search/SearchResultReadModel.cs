namespace Cratebase.Application.Search;

public sealed record SearchResultReadModel(
    Guid Id,
    string Type,
    string Title,
    string? Subtitle,
    IReadOnlyList<string> MatchedFields);
