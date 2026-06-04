using DiscWeave.Api.Features.ExternalSources;

namespace DiscWeave.Api.Features.Artists;

public sealed record UpdateArtistRequest
{
    public required string Name { get; init; }

    public IReadOnlyList<ExternalSourceReferenceRequest>? ExternalSources { get; init; }
}
