namespace Cratebase.Application.Persistence;

public interface IRepository<TAggregate, in TKey>
{
    Task<TAggregate?> TryFindAsync(TKey key, CancellationToken cancellationToken = default);

    void Add(TAggregate aggregate);

    void Delete(TAggregate aggregate);

    async Task DeleteAsync(TKey key, CancellationToken cancellationToken = default)
    {
        TAggregate aggregate = await TryFindAsync(key, cancellationToken)
            ?? throw new ArgumentException(
                $"Aggregate '{typeof(TAggregate).Name}' with key '{key}' was not found",
                nameof(key));

        Delete(aggregate);
    }
}
