using Cratebase.Application.Catalog.Artists;
using Cratebase.Application.Security;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence.Queries;

public sealed class ArtistQueries : IArtistQueries
{
    private const string ArtistTypePropertyName = "artist_type";
    private readonly CratebaseDbContext _context;
    private readonly CollectionId _collectionId;
    private readonly bool _hasCollection;

    public ArtistQueries(CratebaseDbContext context, ICurrentCollection currentCollection)
    {
        _context = context;
        _collectionId = currentCollection.CollectionId;
        _hasCollection = true;
    }

    public async Task<ArtistReadModel?> TryGetAsync(ArtistId artistId, CancellationToken cancellationToken = default)
    {
        return await ApplyCollectionFilter(_context.Artists.AsNoTracking())
            .Where(artist => artist.Id == artistId)
            .Select(artist => new ArtistReadModel(artist.Id, EF.Property<string>(artist, ArtistTypePropertyName), artist.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ArtistListResult> ListAsync(ArtistListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Artist> artists = ApplyCollectionFilter(_context.Artists.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string searchPattern = $"%{query.Search.Trim()}%";
            artists = artists.Where(artist => EF.Functions.ILike(artist.Name, searchPattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            artists = artists.Where(artist => EF.Property<string>(artist, ArtistTypePropertyName) == query.Type);
        }

        int total = await artists.CountAsync(cancellationToken);
        ArtistReadModel[] items = await artists
            .OrderBy(artist => artist.Name)
            .ThenBy(artist => artist.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(artist => new ArtistReadModel(artist.Id, EF.Property<string>(artist, ArtistTypePropertyName), artist.Name))
            .ToArrayAsync(cancellationToken);

        return new ArtistListResult(items, query.Limit, query.Offset, total);
    }

    private IQueryable<Artist> ApplyCollectionFilter(IQueryable<Artist> artists)
    {
        return _hasCollection
            ? artists.Where(artist => artist.CollectionId == _collectionId)
            : artists;
    }
}
