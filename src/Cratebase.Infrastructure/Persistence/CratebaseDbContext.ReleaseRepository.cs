using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<Release, ReleaseId>
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
