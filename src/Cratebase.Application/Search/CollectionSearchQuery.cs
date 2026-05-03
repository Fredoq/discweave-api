namespace Cratebase.Application.Search;

public sealed record CollectionSearchQuery(string Query, int Limit, int Offset);
