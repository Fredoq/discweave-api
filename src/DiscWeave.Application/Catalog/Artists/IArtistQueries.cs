using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Application.Catalog.Artists;

public interface IArtistQueries
{
    Task<ArtistReadModel?> TryGetAsync(ArtistId artistId, CancellationToken cancellationToken = default);

    Task<ArtistListResult> ListAsync(ArtistListQuery query, CancellationToken cancellationToken = default);
}
