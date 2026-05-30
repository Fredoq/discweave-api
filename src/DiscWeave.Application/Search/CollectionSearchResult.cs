namespace DiscWeave.Application.Search;

public sealed record CollectionSearchResult(IReadOnlyList<SearchResultReadModel> Items, int Limit, int Offset, int Total);
