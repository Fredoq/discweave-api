using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Credits;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<Credit, CreditId>
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
