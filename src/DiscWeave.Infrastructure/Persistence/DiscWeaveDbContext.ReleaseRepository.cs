using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<Release, ReleaseId>
{
    async Task<Release?> IRepository<Release, ReleaseId>.TryFindAsync(
        ReleaseId key,
        CancellationToken cancellationToken)
    {
        return await Releases.FirstOrDefaultAsync(release => release.Id == key, cancellationToken);
    }

    void IRepository<Release, ReleaseId>.Add(Release aggregate)
    {
        _ = Releases.Add(aggregate);
    }

    void IRepository<Release, ReleaseId>.Delete(Release aggregate)
    {
        _ = Releases.Remove(aggregate);
    }
}
