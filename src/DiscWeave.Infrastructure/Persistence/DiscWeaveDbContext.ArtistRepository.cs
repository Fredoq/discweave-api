using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<Artist, ArtistId>
{
    async Task<Artist?> IRepository<Artist, ArtistId>.TryFindAsync(
        ArtistId key,
        CancellationToken cancellationToken)
    {
        return await Artists.FirstOrDefaultAsync(artist => artist.Id == key, cancellationToken);
    }

    void IRepository<Artist, ArtistId>.Add(Artist aggregate)
    {
        _ = Artists.Add(aggregate);
    }

    void IRepository<Artist, ArtistId>.Delete(Artist aggregate)
    {
        _ = Artists.Remove(aggregate);
    }
}
