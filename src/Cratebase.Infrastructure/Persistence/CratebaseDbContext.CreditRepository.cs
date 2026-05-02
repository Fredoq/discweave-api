using Cratebase.Application.Persistence;
using Cratebase.Domain.Credits;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<Credit, CreditId>
{
    async Task<Credit?> IRepository<Credit, CreditId>.TryFindAsync(
        CreditId key,
        CancellationToken cancellationToken)
    {
        return await Credits.FirstOrDefaultAsync(credit => credit.Id == key, cancellationToken);
    }

    void IRepository<Credit, CreditId>.Add(Credit aggregate)
    {
        _ = Credits.Add(aggregate);
    }

    void IRepository<Credit, CreditId>.Delete(Credit aggregate)
    {
        _ = Credits.Remove(aggregate);
    }
}
