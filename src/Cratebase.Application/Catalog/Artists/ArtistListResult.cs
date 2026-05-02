namespace Cratebase.Application.Catalog.Artists;

public sealed record ArtistListResult(IReadOnlyList<ArtistReadModel> Items, int Limit, int Offset, int Total);
