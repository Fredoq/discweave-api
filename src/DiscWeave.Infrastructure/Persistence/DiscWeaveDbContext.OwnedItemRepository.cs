using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Collection;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<OwnedItem, OwnedItemId>
{
    async Task<OwnedItem?> IRepository<OwnedItem, OwnedItemId>.TryFindAsync(
        OwnedItemId key,
        CancellationToken cancellationToken)
    {
        return await OwnedItems.FirstOrDefaultAsync(ownedItem => ownedItem.Id == key, cancellationToken);
    }

    void IRepository<OwnedItem, OwnedItemId>.Add(OwnedItem aggregate)
    {
        _ = OwnedItems.Add(aggregate);
    }

    void IRepository<OwnedItem, OwnedItemId>.Delete(OwnedItem aggregate)
    {
        _ = OwnedItems.Remove(aggregate);
    }
}
