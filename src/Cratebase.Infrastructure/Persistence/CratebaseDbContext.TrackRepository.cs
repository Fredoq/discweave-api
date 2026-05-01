using Cratebase.Application.Persistence;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence;

public partial class CratebaseDbContext : IRepository<Track, TrackId>
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
