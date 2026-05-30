using DiscWeave.Application.Persistence;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence;

public partial class DiscWeaveDbContext : IRepository<Track, TrackId>
{
    async Task<Track?> IRepository<Track, TrackId>.TryFindAsync(
        TrackId key,
        CancellationToken cancellationToken)
    {
        return await Tracks.FirstOrDefaultAsync(track => track.Id == key, cancellationToken);
    }

    void IRepository<Track, TrackId>.Add(Track aggregate)
    {
        _ = Tracks.Add(aggregate);
    }

    void IRepository<Track, TrackId>.Delete(Track aggregate)
    {
        _ = Tracks.Remove(aggregate);
    }
}
