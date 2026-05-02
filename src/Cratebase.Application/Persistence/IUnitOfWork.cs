namespace Cratebase.Application.Persistence;

public interface IUnitOfWork
{
    IRepository<TAggregate, TKey> GetRepository<TAggregate, TKey>();

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
