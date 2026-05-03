namespace Cratebase.Application.Search;

public interface ICollectionSearchQueries
{
    Task<CollectionSearchResult> SearchAsync(CollectionSearchQuery query, CancellationToken cancellationToken = default);
}
