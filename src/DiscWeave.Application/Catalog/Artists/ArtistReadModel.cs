using DiscWeave.Domain.Catalog;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Application.Catalog.Artists;

public sealed record ArtistReadModel(
    ArtistId Id,
    string Type,
    string Name,
    IReadOnlyList<ExternalSourceReference> ExternalSources);
