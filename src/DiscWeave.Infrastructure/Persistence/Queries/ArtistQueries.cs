using DiscWeave.Application.Catalog.Artists;
using DiscWeave.Application.Security;
using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace DiscWeave.Infrastructure.Persistence.Queries;

public sealed class ArtistQueries : IArtistQueries
{
    private const string ArtistTypePropertyName = "artist_type";
    private const string ExternalSourcesNavigation = "_externalSources";
    private readonly DiscWeaveDbContext _context;
    private readonly CollectionId _collectionId;
    private readonly bool _hasCollection;

    public ArtistQueries(DiscWeaveDbContext context, ICurrentCollection currentCollection)
    {
        _context = context;
        _collectionId = currentCollection.CollectionId;
        _hasCollection = true;
    }

    public async Task<ArtistReadModel?> TryGetAsync(ArtistId artistId, CancellationToken cancellationToken = default)
    {
        Artist? artist = await ApplyCollectionFilter(_context.Artists.AsNoTracking().Include(ExternalSourcesNavigation))
            .Where(artist => artist.Id == artistId)
            .SingleOrDefaultAsync(cancellationToken);

        return artist is null ? null : ToReadModel(artist);
    }

    public async Task<ArtistListResult> ListAsync(ArtistListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Artist> artists = ApplyCollectionFilter(_context.Artists.AsNoTracking().Include(ExternalSourcesNavigation));

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
        Artist[] items = await artists
            .OrderBy(artist => artist.Name)
            .ThenBy(artist => artist.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToArrayAsync(cancellationToken);

        return new ArtistListResult([.. items.Select(ToReadModel)], query.Limit, query.Offset, total);
    }

    private IQueryable<Artist> ApplyCollectionFilter(IQueryable<Artist> artists)
    {
        return _hasCollection
            ? artists.Where(artist => artist.CollectionId == _collectionId)
            : artists;
    }

    private static ArtistReadModel ToReadModel(Artist artist)
    {
        string type = artist switch
        {
            Person => "person",
            Group => "group",
            _ => throw new InvalidOperationException("Artist type is not supported")
        };

        return new ArtistReadModel(artist.Id, type, artist.Name, artist.ExternalSources);
    }
}
