using Cratebase.Application.Catalog.Artists;
using Cratebase.Domain.Catalog;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Cratebase.Infrastructure.Persistence.Queries;

public sealed class ArtistQueries : IArtistQueries
{
    private const string ArtistTypePropertyName = "artist_type";
    private readonly CratebaseDbContext _context;

    public ArtistQueries(CratebaseDbContext context)
    {
        _context = context;
    }

    public async Task<ArtistReadModel?> TryGetAsync(ArtistId artistId, CancellationToken cancellationToken = default)
    {
        return await _context.Artists
            .AsNoTracking()
            .Where(artist => artist.Id == artistId)
            .Select(artist => new ArtistReadModel(artist.Id, EF.Property<string>(artist, ArtistTypePropertyName), artist.Name))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ArtistListResult> ListAsync(ArtistListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Artist> artists = _context.Artists.AsNoTracking();

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
}
