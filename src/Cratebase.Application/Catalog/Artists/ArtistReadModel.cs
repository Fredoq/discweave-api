using Cratebase.Domain.SharedKernel.Ids;

namespace Cratebase.Application.Catalog.Artists;

public sealed record ArtistReadModel(ArtistId Id, string Type, string Name);
