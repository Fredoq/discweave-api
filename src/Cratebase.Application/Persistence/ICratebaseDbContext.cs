using Cratebase.Domain.Catalog;
using Cratebase.Domain.Collection;
using Cratebase.Domain.Credits;
using Cratebase.Domain.Relations;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Application.Persistence;

public interface ICratebaseDbContext
{
    DbSet<Artist> Artists { get; }

    DbSet<Label> Labels { get; }

    DbSet<Release> Releases { get; }

    DbSet<Track> Tracks { get; }

    DbSet<OwnedItem> OwnedItems { get; }

    DbSet<Credit> Credits { get; }

    DbSet<ArtistRelation> ArtistRelations { get; }

    DbSet<TrackRelation> TrackRelations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
